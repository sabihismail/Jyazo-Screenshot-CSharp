using ScreenShot.src.settings;
using ScreenShot.views.capture;

namespace ScreenShot.src.capture
{
    // ReSharper disable once UnusedType.Global
    public class CaptureWebm : Capture
    {
        public CaptureWebm(Settings settings, Config config) 
        {
            Completed += (_, args) =>
            {
                if (!args.Success) return;
                
                var capturedArea = args.CapturedArea;
                
                var captureWindow = new CaptureWebmWindow(capturedArea, settings, config);

                captureWindow.Show();
            };
        }
    }
}
