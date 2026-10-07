using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BetterScreenShot.Native
{
    // Swallows PrintScreen while the game is focused. Own thread and message loop, so long frames can't get the hook dropped.
    internal static class PrintScreenHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint WM_QUIT = 0x0012;
        private const int VK_SNAPSHOT = 0x2C;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // Static so the GC keeps the delegate alive.
        private static LowLevelKeyboardProc _proc;
        private static Thread _thread;
        private static uint _threadId;
        private static IntPtr _hook;
        private static uint _pid;
        private static int _pressCount;
        private static bool _keyHeld;

        public static volatile bool Intercept;

        public static bool IsInstalled => _hook != IntPtr.Zero;

        public static int LastError { get; private set; }

        public static void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _pid = (uint)Process.GetCurrentProcess().Id;
            _proc = HookCallback;

            var ready = new ManualResetEvent(false);
            _thread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
                if (_hook == IntPtr.Zero)
                {
                    LastError = Marshal.GetLastWin32Error();
                }
                ready.Set();
                if (_hook == IntPtr.Zero)
                {
                    return;
                }

                // Hook callbacks arrive while the thread waits in GetMessage.
                while (GetMessageW(out _, IntPtr.Zero, 0, 0) > 0)
                {
                }

                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            })
            {
                IsBackground = true,
                Name = "BetterScreenShot PrintScreen hook"
            };
            _thread.Start();
            ready.WaitOne(2000);
        }

        public static void Stop()
        {
            if (_thread == null)
            {
                return;
            }

            if (_hook != IntPtr.Zero)
            {
                PostThreadMessageW(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
            _thread.Join(1000);
            _thread = null;
            Interlocked.Exchange(ref _pressCount, 0);
        }

        public static int ConsumePresses() => Interlocked.Exchange(ref _pressCount, 0);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Marshal.ReadInt32(lParam) == VK_SNAPSHOT)
            {
                int msg = wParam.ToInt32();
                bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool wasHeld = _keyHeld;
                _keyHeld = down;

                if (Intercept && IsGameForeground())
                {
                    if (down && !wasHeld)
                    {
                        Interlocked.Increment(ref _pressCount);
                    }
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsGameForeground()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
            {
                return false;
            }
            GetWindowThreadProcessId(fg, out uint pid);
            return pid == _pid;
        }
    }
}
