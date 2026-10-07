using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterScreenShot.Native
{
    internal static class GameWindow
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static IntPtr _handle;

        public static bool IsForeground => GetForegroundWindow() == Handle;

        public static IntPtr Handle
        {
            get
            {
                if (_handle == IntPtr.Zero || !IsWindow(_handle))
                {
                    _handle = Find();
                }
                return _handle;
            }
        }

        private static IntPtr Find()
        {
            uint ownPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            var className = new StringBuilder(64);

            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != ownPid || !IsWindowVisible(hWnd))
                {
                    return true;
                }
                className.Length = 0;
                GetClassNameW(hWnd, className, className.Capacity);
                if (className.ToString() != "UnityWndClass")
                {
                    return true;
                }
                found = hWnd;
                return false;
            }, IntPtr.Zero);

            // Fallback, only meaningful on the main thread.
            return found != IntPtr.Zero ? found : GetActiveWindow();
        }
    }
}
