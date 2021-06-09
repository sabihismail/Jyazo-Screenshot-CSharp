using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ScreenShot.src.tools.util;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Color = System.Drawing.Color;
using Factory = SharpDX.DirectWrite.Factory;
using TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode;

namespace ScreenShot.src.tools.gpu
{
    public class Direct2DRenderer {
        /// <summary>
        ///     Gets the size of the buffer brush.
        /// </summary>
        /// <value>
        ///     The size of the buffer brush.
        /// </value>
        public int BufferBrushSize { get; private set; }

        /// <summary>
        ///     Gets the size of the buffer font.
        /// </summary>
        /// <value>
        ///     The size of the buffer font.
        /// </value>
        public int BufferFontSize { get; private set; }

        /// <summary>
        ///     Gets the size of the buffer layout.
        /// </summary>
        /// <value>
        ///     The size of the buffer layout.
        /// </value>
        public int BufferLayoutSize { get; private set; }

        //transparent background color
        /// <summary>
        ///     The GDI transparent
        /// </summary>
        private static Color gdiTransparent = Color.Transparent;

        /// <summary>
        ///     The transparent
        /// </summary>
        private static readonly RawColor4 TRANSPARENT = new RawColor4(gdiTransparent.R, gdiTransparent.G, gdiTransparent.B,
            gdiTransparent.A);

        //direct x vars
        /// <summary>
        ///     The device
        /// </summary>
        private readonly WindowRenderTarget device;

        /// <summary>
        ///     The factory
        /// </summary>
        private readonly SharpDX.Direct2D1.Factory factory;

        /// <summary>
        ///     The font factory
        /// </summary>
        private readonly Factory fontFactory;

        /// <summary>
        ///     The brush container
        /// </summary>
        private List<SolidColorBrush> brushContainer = new List<SolidColorBrush>(32);

        //thread safe resizing
        /// <summary>
        ///     The do resize
        /// </summary>
        private bool doResize;

        /// <summary>
        ///     The font container
        /// </summary>
        private List<TextFormat> fontContainer = new List<TextFormat>(32);

        /// <summary>
        ///     The layout container
        /// </summary>
        private List<TextLayoutBuffer> layoutContainer = new List<TextLayoutBuffer>(32);

        /// <summary>
        ///     The resize x
        /// </summary>
        private int resizeX;

        /// <summary>
        ///     The resize y
        /// </summary>
        private int resizeY;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Direct2DRenderer" /> class.
        /// </summary>
        /// <param name="hwnd">The HWND.</param>
        /// <param name="limitFps">if set to <c>true</c> [limit FPS].</param>
        public Direct2DRenderer(IntPtr hwnd, bool limitFps) {
            factory = new SharpDX.Direct2D1.Factory();

            fontFactory = new Factory();

            NativeUtils.GetWindowRect(hwnd, out var bounds);

            var targetProperties = new HwndRenderTargetProperties {
                Hwnd = hwnd,
                PixelSize = new Size2(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top),
                PresentOptions = limitFps ? PresentOptions.None : PresentOptions.Immediately
            };

            var prop = new RenderTargetProperties(RenderTargetType.Hardware,
                new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 0, 0, RenderTargetUsage.None,
                FeatureLevel.Level_DEFAULT);

            device = new WindowRenderTarget(factory, prop, targetProperties) {
                TextAntialiasMode = TextAntialiasMode.Aliased,
                AntialiasMode = AntialiasMode.Aliased
            };
        }

        /// <summary>
        ///     Do not call if you use OverlayWindow class
        /// </summary>
        public void Dispose() {
            DeleteBrushContainer();
            DeleteFontContainer();
            DeleteLayoutContainer();

            brushContainer = null;
            fontContainer = null;
            layoutContainer = null;

            fontFactory.Dispose();
            factory.Dispose();
            device.Dispose();
        }

        /// <summary>
        ///     tells renderer to resize when possible
        /// </summary>
        /// <param name="x">Width</param>
        /// <param name="y">Height</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AutoResize(int x, int y) {
            doResize = true;
            resizeX = x;
            resizeY = y;
        }

        /// <summary>
        ///     Call this after EndScene if you created brushes within a loop
        /// </summary>
        public void DeleteBrushContainer() {
            BufferBrushSize = brushContainer.Count;
            foreach (var solidColorBrush in brushContainer) {
                solidColorBrush.Dispose();
            }
            brushContainer = new List<SolidColorBrush>(BufferBrushSize);
        }

        /// <summary>
        ///     Call this after EndScene if you created fonts within a loop
        /// </summary>
        public void DeleteFontContainer() {
            BufferFontSize = fontContainer.Count;
            foreach (var textFormat in fontContainer) {
                textFormat.Dispose();
            }
            fontContainer = new List<TextFormat>(BufferFontSize);
        }

        /// <summary>
        ///     Call this after EndScene if you changed your text's font or have problems with huge memory usage
        /// </summary>
        public void DeleteLayoutContainer() {
            BufferLayoutSize = layoutContainer.Count;
            foreach (var layoutBuffer in layoutContainer) {
                layoutBuffer.Dispose();
            }
            layoutContainer = new List<TextLayoutBuffer>(BufferLayoutSize);
        }

        /// <summary>
        ///     Creates a new SolidColorBrush
        /// </summary>
        /// <param name="color">0x7FFFFFF Premultiplied alpha color</param>
        /// <returns>
        ///     int Brush identifier
        /// </returns>
        public int CreateBrush(int color) {
            brushContainer.Add(new SolidColorBrush(device,
                new RawColor4((color >> 16) & 255L, (color >> 8) & 255L, (byte) color & 255L, (color >> 24) & 255L)));
            return brushContainer.Count - 1;
        }

        /// <summary>
        ///     Creates a new SolidColorBrush. Make sure you applied an alpha value
        /// </summary>
        /// <param name="color">System.Drawing.Color struct</param>
        /// <returns>
        ///     int Brush identifier
        /// </returns>
        public int CreateBrush(Color color) {
            if (color.A == 0) {
                color = Color.FromArgb(255, color);
            }

            brushContainer.Add(new SolidColorBrush(device, new RawColor4(color.R, color.G, color.B, color.A / 255.0f)));
            return brushContainer.Count - 1;
        }

        /// <summary>
        ///     Creates a new Font
        /// </summary>
        /// <param name="fontFamilyName">i.e. Arial</param>
        /// <param name="size">size in units</param>
        /// <param name="bold">print bold text</param>
        /// <param name="italic">print italic text</param>
        /// <returns></returns>
        public int CreateFont(string fontFamilyName, float size, bool bold = false, bool italic = false) {
            fontContainer.Add(new TextFormat(fontFactory, fontFamilyName, bold ? FontWeight.Bold : FontWeight.Normal,
                italic ? FontStyle.Italic : FontStyle.Normal, size));
            return fontContainer.Count - 1;
        }

        /// <summary>
        ///     Do your drawing after this
        /// </summary>
        public void BeginScene() {
            if (doResize) {
                device.Resize(new Size2(resizeX, resizeY));

                doResize = false;
            }
            device.BeginDraw();
        }

        /// <summary>
        ///     Present frame. Do not draw after this.
        /// </summary>
        public void EndScene() {
            device.EndDraw();
            if (!doResize) {
                return;
            }
            device.Resize(new Size2(resizeX, resizeY));

            doResize = false;
        }

        /// <summary>
        ///     Clears the frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearScene() => device.Clear(TRANSPARENT);

        /// <summary>
        ///     Draws the line.
        /// </summary>
        /// <param name="startX">The start x.</param>
        /// <param name="startY">The start y.</param>
        /// <param name="endX">The end x.</param>
        /// <param name="endY">The end y.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLine(int startX, int startY, int endX, int endY, float stroke, int brush) => device.DrawLine(new RawVector2(startX, startY), new RawVector2(endX, endY), brushContainer[brush], stroke);

        /// <summary>
        ///     Draws the rectangle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawRectangle(int x, int y, int width, int height, float stroke, int brush) => device.DrawRectangle(new RawRectangleF(x, y, x + width, y + height), brushContainer[brush], stroke);

        /// <summary>
        ///     Draws the circle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawCircle(int x, int y, int radius, float stroke, int brush) => device.DrawEllipse(new Ellipse(new RawVector2(x, y), radius, radius), brushContainer[brush], stroke);

        /// <summary>
        ///     Draws the box2 d.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="interiorBrush">The interior brush.</param>
        public void DrawBox2D(int x, int y, int width, int height, float stroke, int brush, int interiorBrush) {
            device.DrawRectangle(new RawRectangleF(x, y, x + width, y + height), brushContainer[brush], stroke);
            device.FillRectangle(new RawRectangleF(x + stroke, y + stroke, x + width - stroke, y + height - stroke),
                brushContainer[interiorBrush]);
        }

        /// <summary>
        ///     Draws the box3 d.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="length">The length.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="interiorBrush">The interior brush.</param>
        public void DrawBox3D(int x, int y, int width, int height, int length, float stroke, int brush,
            int interiorBrush) {
            var first = new RawRectangleF(x, y, x + width, y + height);
            var second = new RawRectangleF(x + length, y - length, first.Right + length, first.Bottom - length);

            var lineStart = new RawVector2(x, y);
            var lineEnd = new RawVector2(second.Left, second.Top);

            device.DrawRectangle(first, brushContainer[brush], stroke);
            device.DrawRectangle(second, brushContainer[brush], stroke);

            device.FillRectangle(first, brushContainer[interiorBrush]);
            device.FillRectangle(second, brushContainer[interiorBrush]);

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.X += width;
            lineEnd.X = lineStart.X + length;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.Y += height;
            lineEnd.Y += height;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.X -= width;
            lineEnd.X -= width;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);
        }

        /// <summary>
        ///     Draws the rectangle3 d.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="length">The length.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        public void DrawRectangle3D(int x, int y, int width, int height, int length, float stroke, int brush) {
            var first = new RawRectangleF(x, y, x + width, y + height);
            var second = new RawRectangleF(x + length, y - length, first.Right + length, first.Bottom - length);

            var lineStart = new RawVector2(x, y);
            var lineEnd = new RawVector2(second.Left, second.Top);

            device.DrawRectangle(first, brushContainer[brush], stroke);
            device.DrawRectangle(second, brushContainer[brush], stroke);

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.X += width;
            lineEnd.X = lineStart.X + length;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.Y += height;
            lineEnd.Y += height;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);

            lineStart.X -= width;
            lineEnd.X -= width;

            device.DrawLine(lineStart, lineEnd, brushContainer[brush], stroke);
        }

        /// <summary>
        ///     Draws the plus.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="length">The length.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        public void DrawPlus(int x, int y, int length, float stroke, int brush) {
            var first = new RawVector2(x - length, y);
            var second = new RawVector2(x + length, y);

            var third = new RawVector2(x, y - length);
            var fourth = new RawVector2(x, y + length);

            device.DrawLine(first, second, brushContainer[brush], stroke);
            device.DrawLine(third, fourth, brushContainer[brush], stroke);
        }

        /// <summary>
        ///     Draws the edge.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="length">The length.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        public void DrawEdge(int x, int y, int width, int height, int length, float stroke, int brush) //geht
        {
            var first = new RawVector2(x, y);
            var second = new RawVector2(x, y + length);
            var third = new RawVector2(x + length, y);

            device.DrawLine(first, second, brushContainer[brush], stroke);
            device.DrawLine(first, third, brushContainer[brush], stroke);

            first.Y += height;
            second.Y = first.Y - length;
            third.Y = first.Y;
            third.X = first.X + length;

            device.DrawLine(first, second, brushContainer[brush], stroke);
            device.DrawLine(first, third, brushContainer[brush], stroke);

            first.X = x + width;
            first.Y = y;
            second.X = first.X - length;
            second.Y = first.Y;
            third.X = first.X;
            third.Y = first.Y + length;

            device.DrawLine(first, second, brushContainer[brush], stroke);
            device.DrawLine(first, third, brushContainer[brush], stroke);

            first.Y += height;
            second.X += length;
            second.Y = first.Y - length;
            third.Y = first.Y;
            third.X = first.X - length;

            device.DrawLine(first, second, brushContainer[brush], stroke);
            device.DrawLine(first, third, brushContainer[brush], stroke);
        }

        /// <summary>
        ///     Draws the bar h.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="value">The value.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="interiorBrush">The interior brush.</param>
        public void DrawBarH(int x, int y, int width, int height, float value, float stroke, int brush,
            int interiorBrush) {
            var first = new RawRectangleF(x, y, x + width, y + height);

            device.DrawRectangle(first, brushContainer[brush], stroke);

            if (Math.Abs(value) < 0) {
                return;
            }

            first.Top += height - height / 100.0f * value;

            device.FillRectangle(first, brushContainer[interiorBrush]);
        }

        /// <summary>
        ///     Draws the bar v.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="value">The value.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="interiorBrush">The interior brush.</param>
        public void DrawBarV(int x, int y, int width, int height, float value, float stroke, int brush,
            int interiorBrush) {
            var first = new RawRectangleF(x, y, x + width, y + height);

            device.DrawRectangle(first, brushContainer[brush], stroke);

            if (Math.Abs(value) < 0) {
                return;
            }

            first.Right -= width - width / 100.0f * value;

            device.FillRectangle(first, brushContainer[interiorBrush]);
        }

        /// <summary>
        ///     Fills the rectangle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="brush">The brush.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FillRectangle(int x, int y, int width, int height, int brush) => device.FillRectangle(new RawRectangleF(x, y, x + width, y + height), brushContainer[brush]);

        /// <summary>
        ///     Fills the circle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="brush">The brush.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FillCircle(int x, int y, int radius, int brush) => device.FillEllipse(new Ellipse(new RawVector2(x, y), radius, radius), brushContainer[brush]);

        /// <summary>
        ///     Bordereds the line.
        /// </summary>
        /// <param name="startX">The start x.</param>
        /// <param name="startY">The start y.</param>
        /// <param name="endX">The end x.</param>
        /// <param name="endY">The end y.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="borderBrush">The border brush.</param>
        public void BorderedLine(int startX, int startY, int endX, int endY, float stroke, int brush, int borderBrush) {
            device.DrawLine(new RawVector2(startX, startY), new RawVector2(endX, endY), brushContainer[brush], stroke);

            device.DrawLine(new RawVector2(startX, startY - stroke), new RawVector2(endX, endY - stroke),
                brushContainer[borderBrush], stroke);
            device.DrawLine(new RawVector2(startX, startY + stroke), new RawVector2(endX, endY + stroke),
                brushContainer[borderBrush], stroke);

            device.DrawLine(new RawVector2(startX - stroke / 2, startY - stroke * 1.5f),
                new RawVector2(startX - stroke / 2, startY + stroke * 1.5f), brushContainer[borderBrush], stroke);
            device.DrawLine(new RawVector2(endX - stroke / 2, endY - stroke * 1.5f),
                new RawVector2(endX - stroke / 2, endY + stroke * 1.5f), brushContainer[borderBrush], stroke);
        }

        /// <summary>
        ///     Bordereds the rectangle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="borderStroke">The border stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="borderBrush">The border brush.</param>
        public void BorderedRectangle(int x, int y, int width, int height, float stroke, float borderStroke, int brush,
            int borderBrush) {
            device.DrawRectangle(
                new RawRectangleF(x - (stroke - borderStroke), y - (stroke - borderStroke),
                    x + width + stroke - borderStroke, y + height + stroke - borderStroke), brushContainer[borderBrush],
                borderStroke);

            device.DrawRectangle(new RawRectangleF(x, y, x + width, y + height), brushContainer[brush], stroke);

            device.DrawRectangle(
                new RawRectangleF(x + (stroke - borderStroke), y + (stroke - borderStroke),
                    x + width - stroke + borderStroke, y + height - stroke + borderStroke), brushContainer[borderBrush],
                borderStroke);
        }

        /// <summary>
        ///     Bordereds the circle.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="borderBrush">The border brush.</param>
        public void BorderedCircle(int x, int y, int radius, float stroke, int brush, int borderBrush) {
            device.DrawEllipse(new Ellipse(new RawVector2(x, y), radius + stroke, radius + stroke),
                brushContainer[borderBrush], stroke);

            device.DrawEllipse(new Ellipse(new RawVector2(x, y), radius, radius), brushContainer[brush], stroke);

            device.DrawEllipse(new Ellipse(new RawVector2(x, y), radius - stroke, radius - stroke),
                brushContainer[borderBrush], stroke);
        }

        /// <summary>
        ///     Do not buffer text if you draw i.e. FPS. Use buffer for player names, rank....
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="bufferText">if set to <c>true</c> [buffer text].</param>
        public void DrawText(string text, int font, int brush, int x, int y, bool bufferText = true) {
            if (bufferText) {
                var bufferPos = -1;

                for (var i = 0; i < layoutContainer.Count; i++) {
                    if (layoutContainer[i].Text.Length != text.Length || layoutContainer[i].Text != text) {
                        continue;
                    }
                    bufferPos = i;
                    break;
                }

                if (bufferPos == -1) {
                    layoutContainer.Add(new TextLayoutBuffer(text,
                        new TextLayout(fontFactory, text, fontContainer[font], float.MaxValue, float.MaxValue)));
                    bufferPos = layoutContainer.Count - 1;
                }

                device.DrawTextLayout(new RawVector2(x, y), layoutContainer[bufferPos].TextLayout,
                    brushContainer[brush], DrawTextOptions.NoSnap);
            }
            else {
                var layout = new TextLayout(fontFactory, text, fontContainer[font], float.MaxValue, float.MaxValue);
                device.DrawTextLayout(new RawVector2(x, y), layout, brushContainer[brush]);
                layout.Dispose();
            }
        }

        private class TextLayoutBuffer {
            /// <summary>
            ///     The text
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            ///     The text layout
            /// </summary>
            public TextLayout TextLayout { get; set; }

            /// <summary>
            ///     Initializes a new instance of the <see cref="TextLayoutBuffer" /> class.
            /// </summary>
            /// <param name="text">The text.</param>
            /// <param name="layout">The layout.</param>
            public TextLayoutBuffer(string text, TextLayout layout) {
                Text = text;
                TextLayout = layout;
                TextLayout.TextAlignment = TextAlignment.Leading;
                TextLayout.WordWrapping = WordWrapping.NoWrap;
            }

            /// <summary>
            ///     Releases unmanaged and - optionally - managed resources.
            /// </summary>
            public void Dispose() {
                TextLayout.Dispose();
                Text = null;
            }
        }
    }
}