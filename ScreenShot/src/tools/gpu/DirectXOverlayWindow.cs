using System;
using ScreenShot.src.tools.util;

namespace ScreenShot.src.tools.gpu
{
    public class DirectXOverlayWindow 
    {
        public const int SmCxScreen = 0;

        /// <summary>
        ///     The sm cy screen
        /// </summary>
        public const int SmCyScreen = 1;
        public const uint WindowStyleDx = 0x8000000 //WS_DISABLED
                                          | 0x10000000 //WS_VISIBLE
                                          | 0x80000000; //WS_POPUP

        /// <summary>
        ///     The window ex style dx
        /// </summary>
        public const uint WindowExStyleDx = 0x8000000 //WS_EX_NOACTIVATE
                                            | 0x80000 //WS_EX_LAYERED
                                            | 0x80 //WS_EX_TOOLWINDOW -> Not in taskbar
                                            | 0x8 //WS_EX_TOPMOST
                                            | 0x20; //WS_EX_TRANSPARENT
        
        public const string DesktopClass = "Static"; // System Class for a static control
        
        /// <summary>
        ///     The layered window attribute alpha (LWA_ALPHA)
        /// </summary>
        public const int LwaAlpha = 0x00000002;
        
        /// <summary>
        ///     The HWND topmost
        /// </summary>
        public const int HwndTopmost = -1;

        /// <summary>
        ///     The sw show
        /// </summary>
        public const uint SwShow = 5;

        /// <summary>
        ///     The sw hide
        /// </summary>
        public const uint SwHide = 0;
        
        /// <summary>
        ///     Gets a value indicating whether this instance is disposing.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is disposing; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposing { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether [parent window exists].
        /// </summary>
        /// <value>
        ///     <c>true</c> if [parent window exists]; otherwise, <c>false</c>.
        /// </value>
        public bool ParentWindowExists { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether this instance is top most.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is top most; otherwise, <c>false</c>.
        /// </value>
        public bool IsTopMost { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether this instance is visible.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible { get; private set; }

        /// <summary>
        ///     Gets the x.
        /// </summary>
        /// <value>
        ///     The x.
        /// </value>
        public int X { get; private set; }

        /// <summary>
        ///     Gets the y.
        /// </summary>
        /// <value>
        ///     The y.
        /// </value>
        public int Y { get; private set; }

        /// <summary>
        ///     Gets the width.
        /// </summary>
        /// <value>
        ///     The width.
        /// </value>
        public int Width { get; private set; }

        /// <summary>
        ///     Gets the height.
        /// </summary>
        /// <value>
        ///     The height.
        /// </value>
        public int Height { get; private set; }

        /// <summary>
        ///     Gets the handle.
        /// </summary>
        /// <value>
        ///     The handle.
        /// </value>
        public IntPtr Handle { get; private set; }

        /// <summary>
        ///     Gets the parent window.
        /// </summary>
        /// <value>
        ///     The parent window.
        /// </value>
        public IntPtr ParentWindow { get; }

        /// <summary>
        ///     The margin
        /// </summary>
        private NativeUtils.RawMargin margin;

        /// <summary>
        ///     The graphics
        /// </summary>
        public readonly Direct2DRenderer Graphics;

        /// <summary>
        ///     Makes a transparent Fullscreen window
        /// </summary>
        /// <param name="limitFps">VSync</param>
        /// <exception cref="Exception">Could not create OverlayWindow</exception>
        public DirectXOverlayWindow(bool limitFps = true) {
            IsDisposing = false;
            IsVisible = true;
            IsTopMost = true;

            ParentWindowExists = false;

            X = 0;
            Y = 0;
            Width = NativeUtils.GetSystemMetrics(SmCxScreen);
            Height = NativeUtils.GetSystemMetrics(SmCyScreen);

            ParentWindow = IntPtr.Zero;

            if (!CreateWindow()) {
                throw new Exception("Could not create OverlayWindow");
            }

            Graphics = new Direct2DRenderer(Handle, limitFps);

            SetBounds(X, Y, Width, Height);
        }

        /// <summary>
        ///     Makes a transparent window which adjust it's size and position to fit the parent window
        /// </summary>
        /// <param name="parent">HWND/Handle of a window</param>
        /// <param name="limitFps">VSync</param>
        /// <exception cref="Exception">
        ///     The handle of the parent window isn't valid
        ///     or
        ///     Could not create OverlayWindow
        /// </exception>
        public DirectXOverlayWindow(IntPtr parent, bool limitFps = true) 
        {
            if (parent == IntPtr.Zero) 
            {
                throw new Exception("The handle of the parent window isn't valid");
            }

            NativeUtils.GetWindowRect(parent, out var bounds);

            IsDisposing = false;
            IsVisible = true;
            IsTopMost = true;

            ParentWindowExists = true;

            X = bounds.Left;
            Y = bounds.Top;

            Width = bounds.Right - bounds.Left;
            Height = bounds.Bottom - bounds.Top;

            ParentWindow = parent;

            if (!CreateWindow())
            {
                throw new Exception("Could not create OverlayWindow");
            }

            Graphics = new Direct2DRenderer(Handle, limitFps);

            SetBounds(X, Y, Width, Height);
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="DirectXOverlayWindow" /> class.
        /// </summary>
        ~DirectXOverlayWindow() {
            Dispose();
        }

        /// <summary>
        ///     Clean up used resources and destroy window
        /// </summary>
        public void Dispose() {
            IsDisposing = true;
            Graphics.Dispose();
            NativeUtils.DestroyWindow(Handle);
        }
        
        /// <summary>
        ///     Creates a window with the information's stored in this class
        /// </summary>
        /// <returns>
        ///     true on success
        /// </returns>
        private bool CreateWindow() {
            Handle = NativeUtils.CreateWindowEx(
                WindowExStyleDx,
                DesktopClass,
                "",
                WindowStyleDx,
                X,
                Y,
                Width,
                Height,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (Handle == IntPtr.Zero) {
                return false;
            }

            NativeUtils.SetLayeredWindowAttributes(Handle, 0, 255, LwaAlpha);

            ExtendFrameIntoClient();

            return true;
        }

        /// <summary>
        /// Resize and set new position if the parent window's bounds change
        /// </summary>
        public void Update() {
            if (!ParentWindowExists)
                return;

            NativeUtils.GetWindowRect(ParentWindow, out var bounds);

            if (X != bounds.Left || Y != bounds.Top || Width != bounds.Right - bounds.Left ||
                Height != bounds.Bottom - bounds.Top) {
                SetBounds(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            }
        }

        /// <summary>
        ///     Extends the frame into client.
        /// </summary>
        private void ExtendFrameIntoClient() {
            margin.CxLeftWidth = X;
            margin.CxRightWidth = Width;
            margin.CyBottomHeight = Height;
            margin.CyTopHeight = Y;
            
            NativeUtils.DwmExtendFrameIntoClientArea(Handle, ref margin);
        }

        /// <summary>
        ///     Sets the position.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void SetPos(int x, int y) {
            X = x;
            Y = y;

            NativeUtils.SetWindowPos(Handle, HwndTopmost, X, Y, Width, Height, 0);

            ExtendFrameIntoClient();
        }

        /// <summary>
        ///     Sets the size.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public void SetSize(int width, int height) {
            Width = width;
            Height = height;

            NativeUtils.SetWindowPos(Handle, HwndTopmost, X, Y, Width, Height, 0);

            Graphics.AutoResize(Width, Height);

            ExtendFrameIntoClient();
        }

        /// <summary>
        ///     Sets the bounds.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public void SetBounds(int x, int y, int width, int height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;

            NativeUtils.SetWindowPos(Handle, HwndTopmost, X, Y, Width, Height, 0);

            Graphics?.AutoResize(Width, Height);

            ExtendFrameIntoClient();
        }

        /// <summary>
        ///     Shows this instance.
        /// </summary>
        public void Show() {
            if (IsVisible) {
                return;
            }

            NativeUtils.ShowWindow(Handle, SwShow);
            IsVisible = true;

            ExtendFrameIntoClient();
        }

        /// <summary>
        ///     Hides this instance.
        /// </summary>
        public void Hide() {
            if (!IsVisible) {
                return;
            }

            NativeUtils.ShowWindow(Handle, SwHide);
            IsVisible = false;
        }
    }
}