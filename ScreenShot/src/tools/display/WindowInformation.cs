using System.Collections.Generic;
using System.Text;
using System.Timers;
using ScreenShot.src.capture;
using ScreenShot.src.tools.util;
using ScreenShot.views.capture;
using ScreenShot.views.windows;

namespace ScreenShot.src.tools.display
{
    public static class WindowInformation
    {
        private const int MAX_TITLE_LENGTH = 256;
        private const int TIME_TO_WAIT = 100;

        private static readonly List<string> WINDOWS_TO_IGNORE = new()
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

            timer.Elapsed += (_, _) =>
            {
                var newWindow = GetActiveWindowTitle();

                if (!string.IsNullOrWhiteSpace(newWindow) && newWindow != ActiveWindow && !WINDOWS_TO_IGNORE.Contains(newWindow))
                {
                    ActiveWindow = newWindow;
                }
            };

            timer.Start();
        }

        private static string GetActiveWindowTitle()
        {
            var buffer = new StringBuilder(MAX_TITLE_LENGTH);
            var handle = NativeUtils.GetForegroundWindow();

            return NativeUtils.GetWindowText(handle, buffer, MAX_TITLE_LENGTH) > 0 ? buffer.ToString() : null;
        }
    }
}
