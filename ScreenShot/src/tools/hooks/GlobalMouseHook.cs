using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
// ReSharper disable UnusedMember.Local
// ReSharper disable EventNeverSubscribedTo.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnassignedField.Global

namespace ScreenShot.src.tools.hooks
{
    // Retrieved and modified from
    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/c2506ba7-cfbe-4920-a4eb-71d285a4bb3b/how-do-you-detect-global-mouse-clicks-ie-outside-of-your-form?forum=csharpgeneral
    public class GlobalMouseHook
    {
        private const int WH_MOUSE_LL = 14;

        private const int WM_MOUSEMOVE = 0x200;
        private const int WM_LBUTTONDOWN = 0x201;
        private const int WM_RBUTTONDOWN = 0x204;
        private const int WM_MBUTTONDOWN = 0x207;
        private const int WM_LBUTTONUP = 0x202;
        private const int WM_RBUTTONUP = 0x205;
        private const int WM_MBUTTONUP = 0x208;
        private const int WM_LBUTTONDBLCLK = 0x203;
        private const int WM_RBUTTONDBLCLK = 0x206;
        private const int WM_MBUTTONDBLCLK = 0x209;

        private int hHook;

        public delegate void MouseMoveHandler(object sender, MouseEventArgs e);
        public event MouseMoveHandler MouseMoveEvent;

        public delegate void MouseClickHandler(object sender, MouseEventArgs e);
        public event MouseClickHandler MouseClickEvent;

        public delegate void MouseDownHandler(object sender, MouseEventArgs e);
        public event MouseDownHandler MouseDownEvent;

        public delegate void MouseUpHandler(object sender, MouseEventArgs e);
        public event MouseUpHandler MouseUpEvent;

        private Point point;

        public Point Point
        {
            get => point;
            private set
            {
                if (point == value) return;
                
                point = value;
                
                if (MouseMoveEvent == null) return;
                
                var e = new MouseEventArgs(MouseButtons.None, 0, (int)point.X, (int)point.Y, 0);
                MouseMoveEvent(this, e);
            }
        }

        public GlobalMouseHook()
        {
            Point = new Point();
        }

        private void SetHook()
        {
            hHook = Win32Api.SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, IntPtr.Zero, 0);
        }

        public void SetHookSafe()
        {
            if (hHook != default)
            {
                SetHook();
            }
        }

        public void UnHook()
        {
            Win32Api.UnhookWindowsHookEx(hHook);
        }

        private int MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var mouseHookStruct = (Win32Api.MouseHookStruct)Marshal.PtrToStructure(lParam, typeof(Win32Api.MouseHookStruct));
            if (nCode < 0)
            {
                return Win32Api.CallNextHookEx(hHook, nCode, wParam, lParam);
            }
            
            var button = MouseButtons.None;
            var clickCount = 0;
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                    button = MouseButtons.Left;
                    clickCount = 1;
                    MouseDownEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;

                case WM_RBUTTONDOWN:
                    button = MouseButtons.Right;
                    clickCount = 1;
                    MouseDownEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;

                case WM_MBUTTONDOWN:
                    button = MouseButtons.Middle;
                    clickCount = 1;
                    MouseDownEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;

                case WM_LBUTTONUP:
                    button = MouseButtons.Left;
                    clickCount = 1;
                    MouseUpEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;

                case WM_RBUTTONUP:
                    button = MouseButtons.Right;
                    clickCount = 1;
                    MouseUpEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;

                case WM_MBUTTONUP:
                    button = MouseButtons.Middle;
                    clickCount = 1;
                    MouseUpEvent?.Invoke(this, new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0));
                    break;
            }

            var e = new MouseEventArgs(button, clickCount, (int)point.X, (int)point.Y, 0);
            MouseClickEvent?.Invoke(this, e);

            Point = new Point(mouseHookStruct.pt.X, mouseHookStruct.pt.Y);
            return Win32Api.CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        public static Point GetMousePosition()
        {
            Win32Api.GetCursorPos(out var w32Mouse);

            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        public static class Win32Api
        {
            [StructLayout(LayoutKind.Sequential)]
            public readonly struct Win32Point
            {
                public readonly int X;
                public readonly int Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public class MouseHookStruct
            {
                public Win32Point pt;
                public int hwnd;
                public int wHitTestCode;
                public int dwExtraInfo;
            }

            public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]

            public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);
            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]

            public static extern bool UnhookWindowsHookEx(int idHook);
            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]

            public static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out Win32Point pt);
        }
    }
}
