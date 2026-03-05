using System;
using System.ComponentModel;
using System.Diagnostics;
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

            Debug.WriteLine("[UI] CaptureScreen window created");

            Cursor = Cursors.Cross;

            MOUSE_HOOK.MouseUpExt += GlobalHookMouseUpExt;

            WindowState = WindowState.Maximized;
            Debug.WriteLine("[UI] CaptureScreen window maximized and ready for selection");
        }

        private void GlobalHookMouseUpExt(object sender, MouseEventExtArgs e)
        {
            Debug.WriteLine($"[UI] GlobalHookMouseUpExt triggered at {e.X},{e.Y}");
            MOUSE_HOOK.MouseUpExt -= GlobalHookMouseUpExt;

            Application.Current.Dispatcher.Invoke(Canvas_OnMouseUp);
        }

        private void Canvas_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(Canvas);

            startX = (int)pos.X;
            startY = (int)pos.Y;

            Debug.WriteLine($"[UI] MouseDown at {startX},{startY}");

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
            if (rect == null) return;

            if (e.LeftButton == MouseButtonState.Released)
            {
                Debug.WriteLine("[UI] MouseMove detected button released, triggering OnMouseUp");
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
                Debug.WriteLine("[UI] MouseUp with no rect, closing window");
                Close();
                return;
            }

            var pos = rect.PointToScreen(new Point(0, 0));

            var x = (int)pos.X;
            var y = (int)pos.Y;

            CapturedArea = new System.Drawing.Rectangle(x, y, (int)rect.Width, (int)rect.Height);

            Debug.WriteLine($"[UI] MouseUp: final captured area is {x},{y} {(int)rect.Width}x{(int)rect.Height}");
            Debug.WriteLine("[UI] Closing CaptureScreen window");

            Close();
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (rect != null)
            {
                Debug.WriteLine("[UI] Canvas_MouseLeave: selection in progress, ignoring");
                return;
            }

            if (!NativeUtils.GetCursorPos(out var point))
            {
                Debug.WriteLine("[UI] Canvas_MouseLeave: failed to get cursor position");
                return;
            }

            var wpfPoint = new Point(point.X, point.Y);
            var screen = WPFScreen.GetScreenFrom(wpfPoint);
            Debug.WriteLine($"[UI] Canvas_MouseLeave: moving window to screen with cursor at {point.X},{point.Y}");

            screen.MoveWindow(this);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Debug.WriteLine("[UI] Escape key pressed, closing window");
                Close();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Debug.WriteLine("[UI] Window deactivated, reactivating");
            Topmost = false;
            Topmost = true;
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("[UI] Window closing, cleaning up mouse hook");
            MOUSE_HOOK.MouseUpExt -= GlobalHookMouseUpExt;
        }
    }
}
