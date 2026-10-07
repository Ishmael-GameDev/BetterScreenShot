using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BetterScreenShot.Native
{
    // 24-bit CF_DIB (no alpha for apps to misread) plus the "PNG" format.
    internal static class WinClipboard
    {
        private const uint CF_DIB = 8;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const int BitmapInfoHeaderSize = 40;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterClipboardFormatW(string lpszFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        public static void SetImage(IntPtr rgb24, int width, int height, byte[] png, IntPtr owner)
        {
            IntPtr dib = CreateDib(rgb24, width, height);
            IntPtr pngMem = IntPtr.Zero;
            try
            {
                pngMem = CopyToGlobal(png);
                Open(owner);
                try
                {
                    if (!EmptyClipboard())
                    {
                        throw new InvalidOperationException("EmptyClipboard failed, error " + Marshal.GetLastWin32Error());
                    }
                    if (SetClipboardData(CF_DIB, dib) == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("SetClipboardData(CF_DIB) failed, error " + Marshal.GetLastWin32Error());
                    }
                    // Owned by the clipboard now.
                    dib = IntPtr.Zero;

                    uint pngFormat = RegisterClipboardFormatW("PNG");
                    if (pngFormat != 0 && SetClipboardData(pngFormat, pngMem) != IntPtr.Zero)
                    {
                        pngMem = IntPtr.Zero;
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            finally
            {
                if (dib != IntPtr.Zero)
                {
                    GlobalFree(dib);
                }
                if (pngMem != IntPtr.Zero)
                {
                    GlobalFree(pngMem);
                }
            }
        }

        private static void Open(IntPtr owner)
        {
            // Another process may hold it briefly.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(owner))
                {
                    return;
                }
                Thread.Sleep(25);
            }
            throw new InvalidOperationException("OpenClipboard failed, error " + Marshal.GetLastWin32Error());
        }

        private static IntPtr CopyToGlobal(byte[] data)
        {
            IntPtr mem = Alloc(data.Length);
            IntPtr ptr = GlobalLock(mem);
            Marshal.Copy(data, 0, ptr, data.Length);
            GlobalUnlock(mem);
            return mem;
        }

        // Unity and bottom-up DIBs both store the bottom row first; only BGR order and row padding differ.
        private static unsafe IntPtr CreateDib(IntPtr rgb24, int width, int height)
        {
            int stride = (width * 3 + 3) & ~3;
            long pixelBytes = (long)stride * height;
            IntPtr mem = Alloc(BitmapInfoHeaderSize + pixelBytes);
            byte* dst = (byte*)GlobalLock(mem);

            var header = (int*)dst;
            header[0] = BitmapInfoHeaderSize; // biSize
            header[1] = width;                // biWidth
            header[2] = height;               // biHeight (positive = bottom-up)
            ((short*)dst)[6] = 1;             // biPlanes
            ((short*)dst)[7] = 24;            // biBitCount
            header[4] = 0;                    // biCompression = BI_RGB
            header[5] = (int)pixelBytes;      // biSizeImage
            header[6] = 3780;                 // biXPelsPerMeter (96 DPI)
            header[7] = 3780;                 // biYPelsPerMeter
            header[8] = 0;                    // biClrUsed
            header[9] = 0;                    // biClrImportant

            byte* src = (byte*)rgb24;
            byte* rows = dst + BitmapInfoHeaderSize;
            for (int y = 0; y < height; y++)
            {
                byte* s = src + (long)y * width * 3;
                byte* d = rows + (long)y * stride;
                for (int x = 0; x < width; x++)
                {
                    d[0] = s[2];
                    d[1] = s[1];
                    d[2] = s[0];
                    d += 3;
                    s += 3;
                }
                // Row padding.
                for (byte* end = rows + (long)(y + 1) * stride; d < end; d++)
                {
                    *d = 0;
                }
            }

            GlobalUnlock(mem);
            return mem;
        }

        private static IntPtr Alloc(long size)
        {
            IntPtr mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(ulong)size);
            if (mem == IntPtr.Zero)
            {
                throw new OutOfMemoryException("GlobalAlloc failed for " + size + " bytes");
            }
            return mem;
        }
    }
}
