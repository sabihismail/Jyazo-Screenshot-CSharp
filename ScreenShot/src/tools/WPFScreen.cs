using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace ScreenShot.src.tools
{
    // Retreived from https://stackoverflow.com/a/2118993/10887184
    public class WPFScreen
    {
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

        public static WPFScreen Primary
        {
            get { return new WPFScreen(Screen.PrimaryScreen); }
        }

        private readonly Screen screen;

        internal WPFScreen(Screen screen)
        {
            this.screen = screen;
        }

        public Rect DeviceBounds
        {
            get { return GetRect(screen.Bounds); }
        }

        public Rect WorkingArea
        {
            get { return GetRect(screen.WorkingArea); }
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

        public bool IsPrimary
        {
            get { return screen.Primary; }
        }

        public string DeviceName
        {
            get { return screen.DeviceName; }
        }
    }
}
