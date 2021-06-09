using System;
using System.Collections.Concurrent;
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
        public static readonly ConcurrentQueue<IntPtr> SELECTED_APPLICATION_QUEUE = new();

        private const int MAX_QUEUE_COUNT = 100;
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
                var handle = NativeUtils.GetForegroundWindow();

                var buffer = new StringBuilder(MAX_TITLE_LENGTH);
                var title = NativeUtils.GetWindowText(handle, buffer, MAX_TITLE_LENGTH) > 0 ? buffer.ToString() : null;

                if (string.IsNullOrWhiteSpace(title) || title == ActiveWindow || WINDOWS_TO_IGNORE.Contains(title)) return;
                
                ActiveWindow = title;
                    
                SELECTED_APPLICATION_QUEUE.Enqueue(handle);

                if (SELECTED_APPLICATION_QUEUE.Count > MAX_QUEUE_COUNT)
                {
                    SELECTED_APPLICATION_QUEUE.TryDequeue(out _);
                }
            };
            timer.Start();
        }
    }
}
