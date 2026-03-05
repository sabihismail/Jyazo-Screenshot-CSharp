#nullable enable

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Threading;
using Gma.System.MouseKeyHook;
using ScreenShot.src.tools.util;

namespace ScreenShot.src.tools.gpu
{
    public static class CaptureScreenDirectX
    {
        private static int _startX;
        private static int _startY;
        private static int _currentX;
        private static int _currentY;
        private static bool _isDragging;
        private static bool _captureComplete;
        private static Bitmap? _capturedBitmap;
        private static DispatcherTimer? _renderTimer;
        private static DirectXOverlayWindow? _overlay;
        private static IMouseEvents? _mouseHook;

        /// <summary>
        /// Captures a screenshot of the specified window with region selection overlay.
        /// Uses PrintWindow (PW_RENDERFULLCONTENT) to capture without injection.
        /// </summary>
        public static Task<Bitmap?> Capture(IntPtr hwnd)
        {
            return Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[DX] Capture started");
                    _captureComplete = false;
                    _capturedBitmap = null;
                    _isDragging = false;

                    // Create overlay window over the game
                    System.Diagnostics.Debug.WriteLine($"[DX] Creating overlay for hwnd: {hwnd}");
                    _overlay = new DirectXOverlayWindow(hwnd, limitFps: true);
                    System.Diagnostics.Debug.WriteLine("[DX] Overlay created successfully");

                    // Create brushes for rendering
                    var transparentBrush = _overlay.Graphics.CreateBrush(Color.FromArgb(50, 128, 128, 128));
                    var selectionBrush = _overlay.Graphics.CreateBrush(0x7F00FF00); // Semi-transparent green

                    // Setup mouse hook for input
                    System.Diagnostics.Debug.WriteLine("[DX] Setting up mouse hook");
                    _mouseHook = Hook.GlobalEvents();
                    _mouseHook.MouseDown += (_, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[DX] MouseDown at {e.X}, {e.Y}");
                        _startX = e.X;
                        _startY = e.Y;
                        _isDragging = true;
                    };

                    _mouseHook.MouseMove += (_, e) =>
                    {
                        if (_isDragging)
                            System.Diagnostics.Debug.WriteLine($"[DX] MouseMove at {e.X}, {e.Y}");
                        _currentX = e.X;
                        _currentY = e.Y;
                    };

                    _mouseHook.MouseUp += (_, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[DX] MouseUp at {e.X}, {e.Y}");
                        if (_isDragging)
                        {
                            _isDragging = false;
                            // Capture the region using PrintWindow
                            var region = NormalizeRectangle(_startX, _startY, _currentX, _currentY);
                            System.Diagnostics.Debug.WriteLine($"[DX] Capturing region: {region.X},{region.Y} {region.Width}x{region.Height}");
                            _capturedBitmap = CaptureWindowRegion(hwnd, region);
                            _captureComplete = true;
                        }
                    };

                    // Setup render loop (60 FPS)
                    _renderTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
                    };

                    _renderTimer.Tick += (_, _) =>
                    {
                        try
                        {
                            if (!_overlay.IsVisible)
                                return;

                            // Update overlay position if parent window moved
                            _overlay.Update();

                            // Render
                            _overlay.Graphics.BeginScene();
                            _overlay.Graphics.ClearScene();

                            // Draw semi-transparent overlay background
                            _overlay.Graphics.FillRectangle(0, 0, _overlay.Width, _overlay.Height, transparentBrush);

                            // Draw selection rectangle if dragging
                            if (_isDragging)
                            {
                                var rect = NormalizeRectangle(_startX, _startY, _currentX, _currentY);
                                _overlay.Graphics.DrawBox2D(
                                    rect.X, rect.Y, rect.Width, rect.Height,
                                    stroke: 2, brush: selectionBrush, interiorBrush: transparentBrush);
                            }

                            _overlay.Graphics.EndScene();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DX] Render error: {ex}");
                            _captureComplete = true;
                        }
                    };

                    System.Diagnostics.Debug.WriteLine("[DX] Showing overlay and starting render timer");
                    _overlay.Show();
                    _renderTimer.Start();

                    // Wait for capture to complete
                    System.Diagnostics.Debug.WriteLine("[DX] Waiting for capture...");
                    while (!_captureComplete)
                    {
                        System.Threading.Thread.Sleep(50);
                    }

                    System.Diagnostics.Debug.WriteLine("[DX] Capture complete, stopping timer");
                    _renderTimer.Stop();
                    _overlay.Dispose();

                    System.Diagnostics.Debug.WriteLine($"[DX] Returning bitmap: {(_capturedBitmap != null ? "yes" : "null")}");
                    return _capturedBitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Capture error: {ex}");
                    _overlay?.Dispose();
                    return null;
                }
            });
        }

        /// <summary>
        /// Captures a specific region of a window using PrintWindow.
        /// PrintWindow with PW_RENDERFULLCONTENT flag works with DX9-12, OpenGL, and Vulkan.
        /// </summary>
        private static Bitmap CaptureWindowRegion(IntPtr hwnd, Rectangle region)
        {
            // First get the full window and crop the region
            var bitmap = CaptureWindow(hwnd);
            if (bitmap == null)
                return new Bitmap(1, 1);

            // Ensure region is within bounds
            var clipped = Rectangle.Intersect(region, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            if (clipped.Width <= 0 || clipped.Height <= 0)
                return bitmap;

            // Crop to selected region
            var cropped = bitmap.Clone(clipped, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            bitmap.Dispose();
            return cropped;
        }

        /// <summary>
        /// Captures the entire window using PrintWindow (PW_RENDERFULLCONTENT).
        /// This flag forces Windows to ask the target process to render its content.
        /// Works with any graphics API (DX9-12, OpenGL, Vulkan) without injection.
        /// </summary>
        private static Bitmap CaptureWindow(IntPtr hwnd)
        {
            NativeUtils.GetWindowRect(hwnd, out var rect);
            var width = rect.Width;
            var height = rect.Height;

            if (width <= 0 || height <= 0)
                return new Bitmap(1, 1);

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            var dc = g.GetHdc();

            try
            {
                const uint PW_RENDERFULLCONTENT = 0x02;
                NativeUtils.PrintWindow(hwnd, dc, PW_RENDERFULLCONTENT);
            }
            finally
            {
                g.ReleaseHdc(dc);
            }

            return bmp;
        }

        /// <summary>
        /// Captures the entire fullscreen window without overlay UI.
        /// </summary>
        public static Task<Bitmap?> CaptureFullWindow(IntPtr hwnd)
        {
            return Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DX] CaptureFullWindow for hwnd: {hwnd}");
                    var bitmap = CaptureWindow(hwnd);
                    System.Diagnostics.Debug.WriteLine($"[DX] CaptureFullWindow returned: {(bitmap != null ? "bitmap" : "null")}");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DX] CaptureFullWindow error: {ex}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Clears any existing overlay windows and cleans up resources.
        /// </summary>
        public static void ClearOverlayWindows()
        {
            try
            {
                _renderTimer?.Stop();
                _renderTimer = null;
                _mouseHook = null;
                _overlay?.Dispose();
                _overlay = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing overlay: {ex}");
            }
        }

        /// <summary>
        /// Normalizes a rectangle so that x1,y1 is the top-left and x2,y2 is the bottom-right.
        /// </summary>
        private static Rectangle NormalizeRectangle(int x1, int y1, int x2, int y2)
        {
            var left = Math.Min(x1, x2);
            var top = Math.Min(y1, y2);
            var right = Math.Max(x1, x2);
            var bottom = Math.Max(y1, y2);

            return new Rectangle(left, top, right - left, bottom - top);
        }
    }
}
