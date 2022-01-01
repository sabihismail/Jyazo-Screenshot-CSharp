using System;
using System.Text;
using System.Timers;
using ScreenShot.src.tools.util;

namespace ScreenShot.src.tools.display
{
    public static class WindowHistory
    {
        public static readonly ConcurrentLinkedList<WindowHistoryItem> APPLICATION_HISTORY = new(100);

        public static string LastWindowTitle => APPLICATION_HISTORY.Count > 0 ? APPLICATION_HISTORY.Last.Name : null;

        private const int MAX_TITLE_LENGTH = 256;
        private const int TIME_TO_WAIT = 100;

        public static void BeginObservingWindows()
        {
            var timer = new Timer(TIME_TO_WAIT);

            timer.Elapsed += (_, _) =>
            {
                var handle = NativeUtils.GetForegroundWindow();

                var buffer = new StringBuilder(MAX_TITLE_LENGTH);
                var title = NativeUtils.GetWindowText(handle, buffer, MAX_TITLE_LENGTH) > 0 ? buffer.ToString() : null;

                if (string.IsNullOrWhiteSpace(title) || title.Contains("Jyazo") || APPLICATION_HISTORY.Last?.Name == title) return;
                
                var item = new WindowHistoryItem
                {
                    HWND = handle,
                    Name = title
                };
                
                APPLICATION_HISTORY.AddLast(item);
            };
            
            timer.Start();
        }

        public class WindowHistoryItem
        {
            public IntPtr HWND { get; set; }

            public string Name { get; set; }
        }
    }
}
