using System;
using ScreenShot.views.capture;
using Rectangle = System.Drawing.Rectangle;

namespace ScreenShot.src.capture
{
    public abstract class Capture
    {
        public event EventHandler<CaptureCompletedEventArgs> Completed;

        private readonly CaptureScreen captureScreen = new();

        protected Capture()
        {
            captureScreen.Closed += (sender, _) =>
            {
                if (sender is not CaptureScreen screen) return;

                var capturedArea = screen.CapturedArea;

                OnCompleted(new CaptureCompletedEventArgs
                {
                    Success = capturedArea.HasValue,
                    CapturedArea = capturedArea ?? new Rectangle()
                });
            };
        }

        public void Show()
        {
            captureScreen.Show();
        }

        private void OnCompleted(CaptureCompletedEventArgs e)
        {
            Completed?.Invoke(this, e);
        }
    }

    public class CaptureCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        
        public Rectangle CapturedArea { get; set; }
    }
}