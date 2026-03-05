using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScreenShot.src.settings;
using ScreenShot.src.upload;

namespace ScreenShot.src.capture
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CaptureImage : Capture
    {
        private readonly Settings settings;

        public CaptureImage(Settings settings, Config config)
        {
            this.settings = settings;

            Debug.WriteLine("[CAPTURE] CaptureImage instantiated");

            Completed += (_, args) =>
            {
                Debug.WriteLine($"[CAPTURE] CaptureImage completed, success={args.Success}");

                if (!args.Success)
                {
                    Debug.WriteLine("[CAPTURE] Capture was not successful, aborting");
                    return;
                }

                var capturedArea = args.CapturedArea;
                Debug.WriteLine($"[CAPTURE] Captured area: {capturedArea.X},{capturedArea.Y} {capturedArea.Width}x{capturedArea.Height}");

                if (capturedArea.Width <= 0 || capturedArea.Height <= 0)
                {
                    Debug.WriteLine("[CAPTURE] Captured area is invalid (zero or negative dimensions)");
                    return;
                }

                var file = Path.GetTempPath() + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
                Debug.WriteLine($"[CAPTURE] Temp file: {file}");

                file = CaptureUsingBMP(file, capturedArea);
                Debug.WriteLine($"[CAPTURE] After BMP capture, file: {file}");

                Debug.WriteLine("[CAPTURE] Starting upload");
                Upload.UploadFile(file, settings, config);
            };
        }

        private string CaptureUsingBMP(string file, Rectangle capturedArea)
        {
            try
            {
                Debug.WriteLine($"[CAPTURE] CaptureUsingBMP starting: {capturedArea.Width}x{capturedArea.Height} at {capturedArea.Left},{capturedArea.Top}");

                var bmp = new Bitmap(capturedArea.Width, capturedArea.Height, PixelFormat.Format32bppPArgb);
                Debug.WriteLine("[CAPTURE] Bitmap created");

                var g = Graphics.FromImage(bmp);
                Debug.WriteLine("[CAPTURE] Graphics object created");

                g.CopyFromScreen(capturedArea.Left, capturedArea.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                Debug.WriteLine("[CAPTURE] Screen content copied to bitmap");

                bmp.Save(file, ImageFormat.Png);
                Debug.WriteLine($"[CAPTURE] Bitmap saved to {file}");

                g.Dispose();
                bmp.Dispose();

                if (!settings.SaveAllImages)
                {
                    Debug.WriteLine("[CAPTURE] SaveAllImages is disabled, using temp file");
                    return file;
                }

                var output = settings.SaveDirectory + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
                Debug.WriteLine($"[CAPTURE] Moving temp file to save directory: {output}");

                File.Move(file, output);
                file = output;
                Debug.WriteLine("[CAPTURE] File moved successfully");

                return file;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CAPTURE] Error in CaptureUsingBMP: {ex}");
                throw;
            }
        }
    }
}