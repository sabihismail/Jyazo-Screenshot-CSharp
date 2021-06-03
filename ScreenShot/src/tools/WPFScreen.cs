using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace ScreenShot.src.tools
{
    // Retreived from https://stackoverflow.com/a/2118993/10887184
    public class WPFScreen
    {
        public static WPFScreen Primary => new WPFScreen(Screen.PrimaryScreen);

        public Rect DeviceBounds => GetRect(screen.Bounds);

        public Rect WorkingArea => GetRect(screen.WorkingArea);

        public bool IsPrimary => screen.Primary;

        public string DeviceName => screen.DeviceName;

        private readonly Screen screen;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public static IEnumerable<WPFScreen> AllScreens()
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                yield return new WPFScreen(screen);
            }
        }

        public static Tuple<WPFScreen, IEnumerable<WPFScreen>> AllScreensSeparated()
        {
            var otherScreens = AllScreens().Where(x => x != Primary);

            return Tuple.Create(Primary, otherScreens);
        }
        
        public static WPFScreen GetScreenFrom(Window window)
        {
            WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
            Screen screen = Screen.FromHandle(windowInteropHelper.Handle);
            WPFScreen wpfScreen = new WPFScreen(screen);

            return wpfScreen;
        }

        public static WPFScreen GetScreenFrom(System.Windows.Point point)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);

            // are x,y device-independent-pixels ??
            System.Drawing.Point drawingPoint = new System.Drawing.Point(x, y);
            Screen screen = Screen.FromPoint(drawingPoint);
            WPFScreen wpfScreen = new WPFScreen(screen);

            return wpfScreen;
        }

        public void MoveWindow(Window window)
        {
            var windowInteropHelper = new WindowInteropHelper(window);

            MoveWindow(windowInteropHelper.Handle, (int)DeviceBounds.Left, (int)DeviceBounds.Top, (int)DeviceBounds.Width, (int)DeviceBounds.Height, false);
        }

        internal WPFScreen(Screen screen)
        {
            this.screen = screen;
        }

        private Rect GetRect(Rectangle value)
        {
            // should x, y, width, height be device-independent-pixels ??
            return new Rect
            {
                X = value.X,
                Y = value.Y,
                Width = value.Width,
                Height = value.Height
            };
        }
    }
}
