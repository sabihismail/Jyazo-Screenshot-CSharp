using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using ScreenShot.src.capture;
using ScreenShot.views;

namespace ScreenShot.src.tools
{
    public static class WindowInformation
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private const int MAX_TITLE_LENGTH = 256;
        private const int TIME_TO_WAIT = 100;

        private static readonly List<string> WindowsToIgnore = new List<string>()
        {
            nameof(CaptureScreen),
            nameof(CaptureImage),
            nameof(CaptureGIF),
            nameof(ConfigWindow),
            nameof(SettingsWindow)
        };

        public static string ActiveWindow;

        public static void BeginObservingWindows()
        {
            var timer = new Timer(TIME_TO_WAIT);

            timer.Elapsed += (sender, args) =>
            {
                var newWindow = GetActiveWindowTitle();

                if (!string.IsNullOrWhiteSpace(newWindow) && newWindow != ActiveWindow && !WindowsToIgnore.Contains(newWindow))
                {
                    ActiveWindow = newWindow;
                }
            };

            timer.Start();
        }

        private static string GetActiveWindowTitle()
        {
            var Buff = new StringBuilder(MAX_TITLE_LENGTH);
            var handle = GetForegroundWindow();

            return GetWindowText(handle, Buff, MAX_TITLE_LENGTH) > 0 ? Buff.ToString() : null;
        }
    }
}
