using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BetterScreenShot.Native
{
    // Runs on its own STA thread so the game keeps rendering.
    internal sealed class WinSaveDialog : SaveDialog
    {
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_HIDEREADONLY = 0x00000004;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_EXPLORER = 0x00080000;
        private const int MaxPathChars = 4096;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAMEW
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetSaveFileNameW(ref OPENFILENAMEW ofn);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr reserved, int coInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        public static WinSaveDialog Show(IntPtr owner, string title, string initialDirectory, string fileName)
        {
            var dialog = new WinSaveDialog();
            var thread = new Thread(() => dialog.Run(owner, title, initialDirectory, fileName))
            {
                IsBackground = true,
                Name = "BetterScreenShot save dialog"
            };
            try
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
            catch (Exception)
            {
                // Run() initializes COM itself anyway.
            }
            thread.Start();
            return dialog;
        }

        private void Run(IntPtr owner, string title, string initialDirectory, string fileName)
        {
            // Explorer-style dialogs need STA COM.
            int coResult = CoInitializeEx(IntPtr.Zero, 0x2 /* COINIT_APARTMENTTHREADED */);
            IntPtr buffer = Marshal.AllocHGlobal(MaxPathChars * 2);
            try
            {
                var initial = new char[MaxPathChars];
                fileName.CopyTo(0, initial, 0, Math.Min(fileName.Length, MaxPathChars - 1));
                Marshal.Copy(initial, 0, buffer, MaxPathChars);

                var ofn = new OPENFILENAMEW
                {
                    lStructSize = Marshal.SizeOf(typeof(OPENFILENAMEW)),
                    hwndOwner = owner,
                    lpstrFilter = "PNG (*.png)\0*.png\0\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaxPathChars,
                    lpstrInitialDir = Directory.Exists(initialDirectory) ? initialDirectory : null,
                    lpstrTitle = title,
                    lpstrDefExt = "png",
                    Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR
                };

                if (GetSaveFileNameW(ref ofn))
                {
                    Result = Marshal.PtrToStringUni(buffer);
                }
                else
                {
                    int error = CommDlgExtendedError();
                    if (error != 0)
                    {
                        Error = "GetSaveFileName failed, error 0x" + error.ToString("X");
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                if (coResult >= 0)
                {
                    CoUninitialize();
                }
                Finish();
            }
        }
    }
}
