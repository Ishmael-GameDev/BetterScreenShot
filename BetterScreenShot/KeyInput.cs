using System.Runtime.InteropServices;
using BetterScreenShot.Native;
using UnityEngine;

namespace BetterScreenShot
{
    internal enum Key
    {
        Modifier,
        Shift,
        C,
        S,
        A,
        W,
        D,
        Two,
        Left,
        Right,
        Up,
        Down,
        Escape,
        Space,
        Period,
        Plus,
        Minus,
        F9,
        Backspace
    }

    // Windows reads virtual keys: Unity's letter KeyCodes break under non-Latin layouts.
    internal static class KeyInput
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static readonly int[][] VirtualKeys =
        {
            new[] { 0x11 },       // Modifier: VK_CONTROL
            new[] { 0x10 },       // Shift: VK_SHIFT
            new[] { 0x43 },       // C
            new[] { 0x53 },       // S
            new[] { 0x41 },       // A
            new[] { 0x57 },       // W
            new[] { 0x44 },       // D
            new[] { 0x32 },       // 2
            new[] { 0x25 },       // Left
            new[] { 0x27 },       // Right
            new[] { 0x26 },       // Up
            new[] { 0x28 },       // Down
            new[] { 0x1B },       // Escape
            new[] { 0x20 },       // Space
            new[] { 0xBE },       // VK_OEM_PERIOD
            new[] { 0xBB, 0x6B }, // VK_OEM_PLUS, VK_ADD
            new[] { 0xBD, 0x6D }, // VK_OEM_MINUS, VK_SUBTRACT
            new[] { 0x78 },       // F9
            new[] { 0x08 }        // Backspace
        };

        private static readonly KeyCode[][] KeyCodes =
        {
            Os.IsMac
                ? new[] { KeyCode.LeftCommand, KeyCode.RightCommand }
                : new[] { KeyCode.LeftControl, KeyCode.RightControl },
            new[] { KeyCode.LeftShift, KeyCode.RightShift },
            new[] { KeyCode.C },
            new[] { KeyCode.S },
            new[] { KeyCode.A },
            new[] { KeyCode.W },
            new[] { KeyCode.D },
            new[] { KeyCode.Alpha2 },
            new[] { KeyCode.LeftArrow },
            new[] { KeyCode.RightArrow },
            new[] { KeyCode.UpArrow },
            new[] { KeyCode.DownArrow },
            new[] { KeyCode.Escape },
            new[] { KeyCode.Space },
            new[] { KeyCode.Period },
            new[] { KeyCode.Equals, KeyCode.Plus, KeyCode.KeypadPlus },
            new[] { KeyCode.Minus, KeyCode.KeypadMinus },
            new[] { KeyCode.F9 },
            new[] { KeyCode.Backspace }
        };

        private static readonly bool[] Held = new bool[VirtualKeys.Length];
        private static readonly bool[] WasHeld = new bool[VirtualKeys.Length];
        private static int _lastFrame = -1;

        public static void Update()
        {
            if (_lastFrame == Time.frameCount)
            {
                return;
            }
            _lastFrame = Time.frameCount;

            bool useVirtualKeys = Os.IsWindows;
            // GetAsyncKeyState is global; ignore input meant for other windows and our dialog.
            bool focused = !useVirtualKeys || GameWindow.IsForeground;
            for (int i = 0; i < Held.Length; i++)
            {
                WasHeld[i] = Held[i];
                Held[i] = focused && (useVirtualKeys ? AnyVirtualKey(VirtualKeys[i]) : AnyKeyCode(KeyCodes[i]));
            }
        }

        public static bool IsHeld(Key key) => Held[(int)key];

        public static bool WasPressed(Key key) => Held[(int)key] && !WasHeld[(int)key];

        private static bool AnyVirtualKey(int[] keys)
        {
            foreach (int vk in keys)
            {
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyKeyCode(KeyCode[] keys)
        {
            foreach (KeyCode key in keys)
            {
                if (Input.GetKey(key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
