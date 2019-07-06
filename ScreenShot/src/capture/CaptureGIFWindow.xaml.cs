using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenShot.src.tools;
using ScreenShot.src.upload;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ScreenShot.src.capture
{
    public partial class CaptureGIFWindow
    {
        private const int BUTTON_GAP_X = 40;
        private const int BUTTON_GAP_Y = 10;

        private const int THICKNESS = 1;

        private bool enabled = true;
        private bool paused;
        private bool completed;

        private readonly BitmapImage pauseImage;
        private readonly BitmapImage resumeImage;

        public CaptureGIFWindow(Rectangle capturedArea, Settings settings, Config config)
        {
            pauseImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/pause.png"));
            resumeImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/resume.png"));
            var cancelImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/cancel.png"));
            var completeImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/complete.png"));
            
            InitializeComponent();

            Width = capturedArea.Width + THICKNESS * 2;
            Height = capturedArea.Height + THICKNESS * 2 + cancel.Height + BUTTON_GAP_Y;
            Left = capturedArea.Left - THICKNESS;
            Top = capturedArea.Top - THICKNESS;

            cancel.Source = cancelImage;
            pause.Source = pauseImage;
            complete.Source = completeImage;
            
            Canvas.SetTop(pause, capturedArea.Height + BUTTON_GAP_Y);
            // ReSharper disable once PossibleLossOfFraction
            Canvas.SetLeft(pause, capturedArea.Width / 2 - pauseImage.Width / 2);

            Canvas.SetTop(cancel, Canvas.GetTop(pause));
            Canvas.SetLeft(cancel, Canvas.GetLeft(pause) - BUTTON_GAP_X - cancelImage.Width / 2);

            Canvas.SetTop(complete, Canvas.GetTop(pause));
            Canvas.SetLeft(complete, Canvas.GetLeft(pause) + BUTTON_GAP_X + completeImage.Width / 2);

            var rect = new System.Windows.Shapes.Rectangle
            {
                StrokeThickness = THICKNESS,
                Stroke = new SolidColorBrush(Colors.Black),
                Width = capturedArea.Width + THICKNESS * 2,
                Height = capturedArea.Height + THICKNESS * 2
            };

            canvas.Children.Add(rect);

            Task.Run(() =>
            {
                StartCapture(capturedArea, settings, config);
            });
        }

        private void StartCapture(Rectangle capturedArea, Settings settings, Config config)
        {
            var file = Path.GetTempPath() + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".gif";

            var tasks = new List<Task>();
            using (var writer = new GIFWriter(file, 100, 0))
            {
                var i = 0;
                while (enabled)
                {
                    while (paused)
                    {
                        Thread.Sleep(500);
                    }

                    var task = Task.Run(() =>
                    {
                        var bitmap = CaptureUsingBMP(capturedArea);

                        writer.WriteFrame(bitmap);

                        Interlocked.Increment(ref i);
                    });

                    tasks.Add(task);

                    Thread.Sleep(100);
                }
            }

            if (tasks.Any(x => !x.IsCompleted))
            {
                Thread.Sleep(100);
            }

            if (completed)
            {
                Upload.UploadFile(file, settings, config);
            }
        }

        private static Bitmap CaptureUsingBMP(Rectangle capturedArea)
        {
            var bmp = new Bitmap(capturedArea.Width, capturedArea.Height, PixelFormat.Format32bppPArgb);
            var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(capturedArea.Left, capturedArea.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            return bmp;
        }

        private void Cancel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            enabled = false;

            Close();
        }

        private void Pause_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            paused = !paused;

            pause.Source = paused ? resumeImage : pauseImage;
        }

        private void Complete_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            completed = true;
            enabled = false;

            Close();
        }
    }
}
