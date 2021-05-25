using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenShot.src.capture
{
    public partial class CaptureScreen
    {
        public System.Drawing.Rectangle? CapturedArea;

        private Rectangle rect;

        private int startX;
        private int startY;

        public CaptureScreen()
        {
            InitializeComponent();

            Cursor = Cursors.Cross;
        }

        private void Canvas_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(canvas);

            startX = (int)pos.X;
            startY = (int)pos.Y;
            
            rect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128)),
            };
            
            Canvas.SetLeft(rect, startX);
            Canvas.SetTop(rect, startY);

            canvas.Children.Add(rect);
        }

        private void Canvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released || rect == null) return;

            var pos = e.GetPosition(canvas);

            var smallerX = Math.Min(startX, (int) pos.X);
            var largerX = Math.Max(startX, (int)pos.X);
            var smallerY = Math.Min(startY, (int)pos.Y);
            var largerY = Math.Max(startY, (int)pos.Y);

            rect.Width = largerX - smallerX;
            rect.Height = largerY - smallerY;

            Canvas.SetLeft(rect, smallerX);
            Canvas.SetTop(rect, smallerY);
        }

        private void Canvas_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var pos = rect.PointToScreen(new Point(0, 0));

            var x = (int)pos.X;
            var y = (int)pos.Y;

            CapturedArea = new System.Drawing.Rectangle(x, y, (int)rect.Width, (int)rect.Height);

            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
