using ScreenShot.src.settings;
using ScreenShot.views.capture;

namespace ScreenShot.src.capture
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CaptureGIF : Capture
    {
        public CaptureGIF(Settings settings, Config config) 
        {
            Completed += (_, args) =>
            {
                if (!args.Success) return;
                
                var capturedArea = args.CapturedArea;
                
                var captureWindow = new CaptureGIFWindow(capturedArea, settings, config);

                captureWindow.Show();
            };
        }
    }
}
