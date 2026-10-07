using System;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BetterScreenShot.Native
{
    internal static class Os
    {
        public static readonly bool IsWindows =
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        public static readonly bool IsMac =
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

        public static string ModifierName => IsMac ? "Cmd" : "Ctrl";

        public static string ToggleKeyName => IsMac ? "Cmd+Shift+2" : "PrtSc";

        public static unsafe void CopyImage(Texture2D image, byte[] png)
        {
            if (IsWindows)
            {
                NativeArray<byte> raw = image.GetRawTextureData<byte>();
                var pixels = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(raw);
                WinClipboard.SetImage(pixels, image.width, image.height, png, GameWindow.Handle);
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "BetterScreenShot_clipboard.png");
            File.WriteAllBytes(temp, png);
            try
            {
                if (IsMac)
                {
                    Run("osascript", $"-e \"set the clipboard to (read (POSIX file \\\"{temp}\\\") as «class PNGf»)\"");
                }
                // They leave a child serving the clipboard, which would hold a redirected stderr open forever.
                else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                {
                    Run("sh", $"-c \"wl-copy --type image/png < '{temp}'\"", captureErrors: false);
                }
                else
                {
                    Run("xclip", $"-selection clipboard -t image/png -i \"{temp}\"", captureErrors: false);
                }
            }
            finally
            {
                // Already read by the time they detach.
                File.Delete(temp);
            }
        }

        public static SaveDialog ShowSaveDialog(string title, string directory, string fileName)
        {
            if (IsWindows)
            {
                return WinSaveDialog.Show(GameWindow.Handle, title, directory, fileName);
            }
            if (IsMac)
            {
                string script = $"POSIX path of (choose file name with prompt \\\"{Escape(title)}\\\" default name \\\"{Escape(fileName)}\\\""
                    + (Directory.Exists(directory) ? $" default location (POSIX file \\\"{Escape(directory)}\\\")" : "")
                    + ")";
                return ProcessSaveDialog.Show("osascript", $"-e \"{script}\"");
            }
            return ProcessSaveDialog.Show("zenity",
                $"--file-selection --save --confirm-overwrite --title=\"{Escape(title)}\" --filename=\"{Escape(Path.Combine(directory, fileName))}\"");
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void Run(string fileName, string arguments, bool captureErrors = true)
        {
            var info = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = captureErrors,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(info))
            {
                string error = captureErrors ? process.StandardError.ReadToEnd() : "";
                if (!process.WaitForExit(10000))
                {
                    throw new TimeoutException(fileName + " did not finish");
                }
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{fileName} exited with {process.ExitCode}: {error.Trim()}");
                }
            }
        }
    }
}
