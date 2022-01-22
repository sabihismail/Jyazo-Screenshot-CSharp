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
using ScreenShot.src.settings;
using ScreenShot.src.upload;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ScreenShot.views.capture
{
    public partial class CaptureWebmWindow
    {
        private const int BUTTON_GAP_X = 40;
        private const int BUTTON_GAP_Y = 10;

        private const int THICKNESS = 1;

        private const int GIF_GAP = 75;

        private bool enabled = true;
        private bool paused;
        private bool completed;

        private readonly BitmapImage pauseImage;
        private readonly BitmapImage resumeImage;

        public CaptureWebmWindow(Rectangle capturedArea, Settings settings, Config config)
        {
            pauseImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/pause.png"));
            resumeImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/resume.png"));
            var cancelImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/cancel.png"));
            var completeImage = new BitmapImage(new Uri("pack://application:,,,/resources/images/complete.png"));
            
            InitializeComponent();

            Width = capturedArea.Width + THICKNESS * 2;
            Height = capturedArea.Height + THICKNESS * 2 + Cancel.Height + BUTTON_GAP_Y;
            Left = capturedArea.Left - THICKNESS;
            Top = capturedArea.Top - THICKNESS;

            Cancel.Source = cancelImage;
            Pause.Source = pauseImage;
            Complete.Source = completeImage;
            
            Canvas.SetTop(Pause, capturedArea.Height + BUTTON_GAP_Y);
            Canvas.SetLeft(Pause, capturedArea.Width / 2.0 - pauseImage.Width / 2);

            Canvas.SetTop(Cancel, Canvas.GetTop(Pause));
            Canvas.SetLeft(Cancel, Canvas.GetLeft(Pause) - BUTTON_GAP_X - cancelImage.Width / 2);

            Canvas.SetTop(Complete, Canvas.GetTop(Pause));
            Canvas.SetLeft(Complete, Canvas.GetLeft(Pause) + BUTTON_GAP_X + completeImage.Width / 2);

            var rect = new System.Windows.Shapes.Rectangle
            {
                StrokeThickness = THICKNESS,
                Stroke = new SolidColorBrush(Colors.Black),
                Width = capturedArea.Width + THICKNESS * 2,
                Height = capturedArea.Height + THICKNESS * 2
            };

            Canvas.Children.Add(rect);

            Task.Run(() =>
            {
                StartCapture(capturedArea, settings, config);
            });
        }

        private void StartCapture(Rectangle capturedArea, Settings settings, Config config)
        {
            var file = Path.GetTempPath() + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".gif";

            var tasks = new List<Task>();
            using (var gifCreator = AnimatedGif.AnimatedGif.Create(file, GIF_GAP))
            {
                var i = 0;
                while (enabled)
                {
                    while (paused)
                    {
                        Thread.Sleep(100);
                    }

                    var task = Task.Run(() =>
                    {
                        var bitmap = CaptureUsingBMP(capturedArea);
                        
                        // ReSharper disable once AccessToDisposedClosure
                        gifCreator.AddFrame(bitmap);

                        Interlocked.Increment(ref i);
                    });

                    tasks.Add(task);

                    Thread.Sleep(GIF_GAP);
                }

                while (tasks.Any(x => !x.IsCompleted))
                {
                    Thread.Sleep(100);
                }
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

            Pause.Source = paused ? resumeImage : pauseImage;
        }

        private void Complete_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            completed = true;
            enabled = false;

            Close();
        }
    }
}
