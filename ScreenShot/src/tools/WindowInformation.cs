using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using ScreenShot.src.capture;

namespace ScreenShot.src.tools
{
    public static class WindowInformation
    {
        private const int MAX_TITLE_LENGTH = 256;
        private const int TIME_TO_WAIT = 100;

        public static string ActiveWindow;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public static void BeginObservingWindows()
        {
            var timer = new Timer(TIME_TO_WAIT);
            timer.Elapsed += (sender, args) =>
            {
                var newWindow = GetActiveWindowTitle();

                if (!string.IsNullOrWhiteSpace(newWindow) && newWindow != ActiveWindow && !newWindow.Contains(nameof(CaptureScreen)))
                {
                    ActiveWindow = Regex.Replace(newWindow, @"\p{C}+", string.Empty);
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
