using System;
using ScreenShot.src.settings;
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
                var capturedArea = ((CaptureScreen) sender).CapturedArea;

                if (capturedArea != null)
                {
                    OnCompleted(new CaptureCompletedEventArgs
                    {
                        CapturedArea = capturedArea.Value
                    });
                }
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
        public Rectangle CapturedArea { get; set; }
    }
}
