using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Gma.System.MouseKeyHook;
using ScreenShot.src.tools.display;
using ScreenShot.src.tools.util;

namespace ScreenShot.views.capture
{
    public partial class CaptureScreen
    {
        private static readonly IKeyboardMouseEvents MOUSE_HOOK = Hook.GlobalEvents();
        
        public System.Drawing.Rectangle? CapturedArea;

        private Rectangle rect;

        private int startX;
        private int startY;

        public CaptureScreen()
        {
            InitializeComponent();

            Cursor = Cursors.Cross;

            MOUSE_HOOK.MouseUpExt += GlobalHookMouseUpExt;
            
            WindowState = WindowState.Maximized;
        }

        private void GlobalHookMouseUpExt(object sender, MouseEventExtArgs e)
        {
            MOUSE_HOOK.MouseUpExt -= GlobalHookMouseUpExt;
            
            Application.Current.Dispatcher.Invoke(Canvas_OnMouseUp);
        }

        private void Canvas_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(Canvas);

            startX = (int)pos.X;
            startY = (int)pos.Y;
            
            rect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128))
            };
            
            Canvas.SetLeft(rect, startX);
            Canvas.SetTop(rect, startY);

            Canvas.Children.Add(rect);
        }

        private void Canvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                if (rect == null) return;

                Canvas_OnMouseUp();
            }

            var mousePosition = e.GetPosition(Canvas);

            var smallerX = Math.Min(startX, (int)mousePosition.X);
            var largerX = Math.Max(startX, (int)mousePosition.X);
            var smallerY = Math.Min(startY, (int)mousePosition.Y);
            var largerY = Math.Max(startY, (int)mousePosition.Y);

            rect.Width = largerX - smallerX;
            rect.Height = largerY - smallerY;

            Canvas.SetLeft(rect, smallerX);
            Canvas.SetTop(rect, smallerY);
        }

        private void Canvas_OnMouseUp()
        {
            if (rect == null)
            {
                Close();
                return;
            }

            var pos = rect.PointToScreen(new Point(0, 0));

            var x = (int)pos.X;
            var y = (int)pos.Y;

            CapturedArea = new System.Drawing.Rectangle(x, y, (int)rect.Width, (int)rect.Height);

            Close();
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (rect != null)
            {
                return;
            }

            if (!NativeUtils.GetCursorPos(out var point)) return;
            
            var wpfPoint = new Point(point.X, point.Y);
            var screen = WPFScreen.GetScreenFrom(wpfPoint);
                
            screen.MoveWindow(this);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = false;
            Topmost = true;
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            MOUSE_HOOK.MouseUpExt -= GlobalHookMouseUpExt;
        }
    }
}
