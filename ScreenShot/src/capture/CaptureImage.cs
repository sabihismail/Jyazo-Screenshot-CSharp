using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScreenShot.src.upload;

namespace ScreenShot.src.capture
{
    public class CaptureImage : Capture
    {
        private readonly Settings settings;
        private readonly Config config;

        public CaptureImage(Settings settings, Config config)
        {
            this.settings = settings;
            this.config = config;

            Completed += (sender, args) =>
            {
                var capturedArea = args.CapturedArea;

                var file = Path.GetTempPath() + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
                file = CaptureUsingBMP(file, capturedArea);

                Upload.UploadFile(file, settings, config);
            };
        }

        private string CaptureUsingBMP(string file, Rectangle capturedArea)
        {
            var bmp = new Bitmap(capturedArea.Width, capturedArea.Height, PixelFormat.Format32bppPArgb);
            var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(capturedArea.Left, capturedArea.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            bmp.Save(file, ImageFormat.Png);

            if (!settings.saveAllImages) return file;

            var output = settings.saveDirectory + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
            File.Move(file, output);
            file = output;

            return file;
        }
    }
}
