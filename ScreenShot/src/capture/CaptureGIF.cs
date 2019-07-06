namespace ScreenShot.src.capture
{
    public class CaptureGIF : Capture
    {
        public CaptureGIF(Settings settings, Config config)
        {
            Completed += (sender, args) =>
            {
                var capturedArea = args.CapturedArea;
                
                var captureWindow = new CaptureGIFWindow(capturedArea, settings, config);

                captureWindow.Show();
            };
        }
    }
}
