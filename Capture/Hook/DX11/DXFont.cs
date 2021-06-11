// Adapted from Frank Luna's "Sprites and Text" example here: http://www.d3dcoder.net/resources.htm 
// checkout his books here: http://www.d3dcoder.net/default.htm

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Color = System.Drawing.Color;
using Device = SharpDX.Direct3D11.Device;
using Rectangle = SharpDX.Rectangle;
// ReSharper disable UnusedMember.Local

namespace Capture.Hook.DX11
{
    public class DXFont : IDisposable
    {
        private Device device;
        // ReSharper disable once NotAccessedField.Local
        private DeviceContext deviceContext;

        public DXFont(Device device, DeviceContext deviceContext)
        {
            this.device = device;
            this.deviceContext = deviceContext;
            initialized = false;
            fontSheetTex = null;
            fontSheetSrv = null;
            texWidth = 1024;
            texHeight = 0;
            spaceWidth = 0;
            charHeight = 0;
        }

        public void Dispose()
        {
            fontSheetTex?.Dispose();
            fontSheetSrv?.Dispose();

            fontSheetTex = null;
            fontSheetSrv = null;
            device = null;
            deviceContext = null;
        }

        // ReSharper disable once UnusedType.Local
        private enum Style
        {
            STYLE_NORMAL = 0,
            STYLE_BOLD = 1,
            STYLE_ITALIC = 2,
            STYLE_BOLD_ITALIC = 3,
            STYLE_UNDERLINE = 4,
            STYLE_STRIKEOUT = 8
        }

        private bool initialized;
        private const char START_CHAR = (char)33;
        private const char END_CHAR = (char)127;
        private const uint NUM_CHARS = END_CHAR - START_CHAR;
        private ShaderResourceView fontSheetSrv;
        private Texture2D fontSheetTex;
        private readonly int texWidth;
        private int texHeight;
        private readonly Rectangle[] charRects = new Rectangle[NUM_CHARS];
        private int spaceWidth, charHeight;

        public bool Initialize(string fontName, float fontSize, FontStyle fontStyle, bool antiAliased)
        {
            Debug.Assert(!initialized);
            var font = new Font(fontName, fontSize, fontStyle, GraphicsUnit.Pixel);

            var hint = antiAliased ? TextRenderingHint.AntiAlias : TextRenderingHint.SystemDefault;

            var tempSize = (int)(fontSize * 2);
            using (var charBitmap = new Bitmap(tempSize, tempSize, PixelFormat.Format32bppArgb))
            {
                using (var charGraphics = Graphics.FromImage(charBitmap))
                {
                    charGraphics.PageUnit = GraphicsUnit.Pixel;
                    charGraphics.TextRenderingHint = hint;

                    MeasureChars(font, charGraphics);

                    using (var fontSheetBitmap = new Bitmap(texWidth, texHeight, PixelFormat.Format32bppArgb))
                    {
                        using (var fontSheetGraphics = Graphics.FromImage(fontSheetBitmap))
                        {
                            fontSheetGraphics.CompositingMode = CompositingMode.SourceCopy;
                            fontSheetGraphics.Clear(Color.FromArgb(0, Color.Black));

                            BuildFontSheetBitmap(font, charGraphics, charBitmap, fontSheetGraphics);

                            if (!BuildFontSheetTexture(fontSheetBitmap))
                            {
                                return false;
                            }
                        }
                        //System.Drawing.Bitmap bm = new System.Drawing.Bitmap(fontSheetBitmap);
                        //bm.Save(@"C:\temp\test.png");
                    }
                }
            }

            initialized = true;

            return true;
        }

        private bool BuildFontSheetTexture(Bitmap fontSheetBitmap)
        {
            var bmData = fontSheetBitmap.LockBits(new System.Drawing.Rectangle(0, 0, texWidth, texHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var texDesc = new Texture2DDescription
            {
                Width = texWidth,
                Height = texHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = {Count = 1, Quality = 0},
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            DataBox data;
            data.DataPointer = bmData.Scan0;
            data.RowPitch = texWidth * 4;
            data.SlicePitch = 0;

            fontSheetTex = new Texture2D(device, texDesc, new[] { data });
            if (fontSheetTex == null)
                return false;
            
            var srvDesc = new ShaderResourceViewDescription
            {
                Format = Format.B8G8R8A8_UNorm,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D =
                {
                    MipLevels = 1, 
                    MostDetailedMip = 0
                }
            };

            fontSheetSrv = new ShaderResourceView(device, fontSheetTex, srvDesc);
            if (fontSheetSrv == null)
                return false;

            fontSheetBitmap.UnlockBits(bmData);

            return true;
        }

        private void MeasureChars(Font font, Graphics charGraphics)
        {
            var allChars = new char[NUM_CHARS];

            for (var i = (char)0; i < NUM_CHARS; ++i)
                allChars[i] = (char)(START_CHAR + i);

            var size = charGraphics.MeasureString(new string(allChars), font, new PointF(0, 0), StringFormat.GenericDefault);

            charHeight = (int)(size.Height + 0.5f);

            var numRows = (int)(size.Width / texWidth) + 1;
            texHeight = numRows * charHeight + 1;

            var sf = StringFormat.GenericDefault;
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            size = charGraphics.MeasureString(" ", font, 0, sf);
            spaceWidth = (int)(size.Width + 0.5f);
        }

        private void BuildFontSheetBitmap(Font font, Graphics charGraphics, Bitmap charBitmap, Graphics fontSheetGraphics)
        {
            var whiteBrush = Brushes.White;
            var fontSheetX = 0;
            var fontSheetY = 0;

            for (var i = 0; i < NUM_CHARS; ++i)
            {
                charGraphics.Clear(Color.FromArgb(0, Color.Black));
                charGraphics.DrawString(((char)(START_CHAR + i)).ToString(), font, whiteBrush, new PointF(0.0f, 0.0f));

                var minX = GetCharMinX(charBitmap);
                var maxX = GetCharMaxX(charBitmap);
                var charWidth = maxX - minX + 1;

                if (fontSheetX + charWidth >= texWidth)
                {
                    fontSheetX = 0;
                    fontSheetY += charHeight + 1;
                }

                charRects[i] = new Rectangle(fontSheetX, fontSheetY, charWidth, charHeight);

                fontSheetGraphics.DrawImage(charBitmap, fontSheetX, fontSheetY, new System.Drawing.Rectangle(minX, 0, charWidth, charHeight), GraphicsUnit.Pixel);

                fontSheetX += charWidth + 1;
            }
        }

        private static int GetCharMaxX(Bitmap charBitmap)
        {
            var width = charBitmap.Width;
            var height = charBitmap.Height;

            for (var x = width - 1; x >= 0; --x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var color = charBitmap.GetPixel(x, y);
                    
                    if (color.A > 0)
                        return x;
                }
            }

            return width - 1;
        }

        private static int GetCharMinX(Bitmap charBitmap)
        {
            var width = charBitmap.Width;
            var height = charBitmap.Height;

            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var color = charBitmap.GetPixel(x, y);
                    
                    if (color.A > 0)
                        return x;
                }
            }

            return 0;
        }

        public ShaderResourceView GetFontSheetSrv()
        {
            Debug.Assert(initialized);

            return fontSheetSrv;
        }

        public Rectangle GetCharRect(char c)
        {
            Debug.Assert(initialized);

            return charRects[c - START_CHAR];
        }

        public int GetSpaceWidth()
        {
            Debug.Assert(initialized);

            return spaceWidth;
        }

        public int GetCharHeight()
        {
            Debug.Assert(initialized);

            return charHeight;
        }
    }
}


