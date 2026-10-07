using System;
using System.Diagnostics;
using System.Threading;

namespace BetterScreenShot.Native
{
    // osascript/zenity print the chosen path; a non-zero exit code means cancelled.
    internal sealed class ProcessSaveDialog : SaveDialog
    {
        public static ProcessSaveDialog Show(string fileName, string arguments)
        {
            var dialog = new ProcessSaveDialog();
            var thread = new Thread(() => dialog.Run(fileName, arguments))
            {
                IsBackground = true,
                Name = "BetterScreenShot save dialog"
            };
            thread.Start();
            return dialog;
        }

        private void Run(string fileName, string arguments)
        {
            try
            {
                var info = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    if (process.ExitCode == 0 && output.Length > 0)
                    {
                        Result = output.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? output : output + ".png";
                    }
                }
            }
            catch (Exception e)
            {
                Error = $"{fileName}: {e.Message}";
            }
            finally
            {
                Finish();
            }
        }
    }
}
