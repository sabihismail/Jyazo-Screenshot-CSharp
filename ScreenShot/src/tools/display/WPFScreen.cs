using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
// ReSharper disable UnusedMember.Global

namespace ScreenShot.src.tools.display
{
    // Retrieved from https://stackoverflow.com/a/2118993/10887184
    public class WPFScreen
    {
        private static WPFScreen Primary => new(Screen.PrimaryScreen);

        private Rect DeviceBounds => GetRect(screen.Bounds);

        public Rect WorkingArea => GetRect(screen.WorkingArea);

        public bool IsPrimary => screen.Primary;

        public string DeviceName => screen.DeviceName;

        private readonly Screen screen;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        private static IEnumerable<WPFScreen> AllScreens()
        {
            return Screen.AllScreens.Select(screen => new WPFScreen(screen));
        }

        public static Tuple<WPFScreen, IEnumerable<WPFScreen>> AllScreensSeparated()
        {
            var otherScreens = AllScreens().Where(x => x != Primary);

            return Tuple.Create(Primary, otherScreens);
        }
        
        public static WPFScreen GetScreenFrom(Window window)
        {
            var windowInteropHelper = new WindowInteropHelper(window);
            var screen = Screen.FromHandle(windowInteropHelper.Handle);
            var wpfScreen = new WPFScreen(screen);

            return wpfScreen;
        }

        public static WPFScreen GetScreenFrom(System.Windows.Point point)
        {
            var x = (int)Math.Round(point.X);
            var y = (int)Math.Round(point.Y);

            // are x,y device-independent-pixels ??
            var drawingPoint = new System.Drawing.Point(x, y);
            var screen = Screen.FromPoint(drawingPoint);
            var wpfScreen = new WPFScreen(screen);

            return wpfScreen;
        }

        public void MoveWindow(Window window)
        {
            var windowInteropHelper = new WindowInteropHelper(window);

            MoveWindow(windowInteropHelper.Handle, (int)DeviceBounds.Left, (int)DeviceBounds.Top, (int)DeviceBounds.Width, (int)DeviceBounds.Height, false);
        }

        private WPFScreen(Screen screen)
        {
            this.screen = screen;
        }

        private static Rect GetRect(Rectangle value)
        {
            // should x, y, width, height be device-independent-pixels ??
            return new()
            {
                X = value.X,
                Y = value.Y,
                Width = value.Width,
                Height = value.Height
            };
        }
    }
}
