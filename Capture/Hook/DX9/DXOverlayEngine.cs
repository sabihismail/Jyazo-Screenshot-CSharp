using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Capture.Hook.Common;
using SharpDX;
using SharpDX.Direct3D9;
using Font = SharpDX.Direct3D9.Font;

namespace Capture.Hook.DX9
{
    internal class DXOverlayEngine : Component
    {
        public List<IOverlay> Overlays { get; private set; }

        private bool initialised;
        private bool initialising;

        private Sprite sprite;
        private readonly Dictionary<string, Font> fontCache = new();
        private readonly Dictionary<Element, Texture> imageCache = new();

        public Device Device { get; private set; }

        public DXOverlayEngine()
        {
            Overlays = new List<IOverlay>();
        }

        private void EnsureInitialized()
        {
            Debug.Assert(initialised);
        }

        public bool Initialise(Device device)
        {
            Debug.Assert(!initialised);
            if (initialising)
                return false;

            initialising = true;

            try
            {

                Device = device;

                sprite = ToDispose(new Sprite(Device));

                // Initialise any resources required for overlay elements
                InitializeElementResources();

                initialised = true;
                return true;
            }
            finally
            {
                initialising = false;
            }
        }

        private void InitializeElementResources()
        {
            foreach (var element in Overlays.SelectMany(overlay => overlay.Elements))
            {
                switch (element)
                {
                    case TextElement textElement:
                        GetFontForTextElement(textElement);
                        break;
                        
                    case ImageElement imageElement:
                        GetImageForImageElement(imageElement);
                        break;
                }
            }
        }

        private void Begin()
        {
            sprite.Begin(SpriteFlags.AlphaBlend);
        }

        /// <summary>
        /// Draw the overlay(s)
        /// </summary>
        public void Draw()
        {
            EnsureInitialized();

            Begin();

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var overlay in Overlays)
            {
                foreach (var element in overlay.Elements.Where(element => !element.Hidden))
                {
                    switch (element)
                    {
                        case TextElement textElement:
                        {
                            var font = GetFontForTextElement(textElement);
                            if (font != null && !string.IsNullOrEmpty(textElement.Text))
                                font.DrawText(sprite, textElement.Text, textElement.Location.X, textElement.Location.Y, new ColorBGRA(textElement.Color.R, 
                                    textElement.Color.G, textElement.Color.B, textElement.Color.A));
                            break;
                        }
                        case ImageElement imageElement:
                        {
                            //Apply the scaling of the imageElement
                            var rotation = Matrix.RotationZ(imageElement.Angle);
                            var scaling = Matrix.Scaling(imageElement.Scale);
                            sprite.Transform = rotation * scaling;

                            var image = GetImageForImageElement(imageElement);
                            if (image != null)
                                sprite.Draw(image, new ColorBGRA(imageElement.Tint.R, imageElement.Tint.G, imageElement.Tint.B, imageElement.Tint.A), 
                                    null, null, new Vector3(imageElement.Location.X, imageElement.Location.Y, 0));

                            //Reset the transform for other elements
                            sprite.Transform = Matrix.Identity;
                            break;
                        }
                    }
                }
            }

            End();
        }

        private void End()
        {
            sprite.End();
        }

        /// <summary>
        /// In Direct3D9 it is necessary to call OnLostDevice before any call to device.Reset(...) for certain interfaces found in D3DX (e.g. ID3DXSprite, ID3DXFont, ID3DXLine) - https://msdn.microsoft.com/en-us/library/windows/desktop/bb172979(v=vs.85).aspx
        /// </summary>
        public void BeforeDeviceReset()
        {
            try
            {
                foreach (var item in fontCache)
                    item.Value.OnLostDevice();

                sprite?.OnLostDevice();
            }
            catch
            {
                // ignored
            }
        }

        private Font GetFontForTextElement(TextElement element)
        {
            var fontKey = $"{element.Font.Name}{element.Font.Size}{element.Font.Style}{element.AntiAliased}";

            if (fontCache.TryGetValue(fontKey, out var result)) return result;
            
            result = ToDispose(new Font(Device, new FontDescription { 
                FaceName = element.Font.Name,
                Italic = (element.Font.Style & FontStyle.Italic) == FontStyle.Italic,
                Quality = element.AntiAliased ? FontQuality.Antialiased : FontQuality.Default,
                Weight = (element.Font.Style & FontStyle.Bold) == FontStyle.Bold ? FontWeight.Bold : FontWeight.Normal,
                Height = (int)element.Font.SizeInPoints
            }));
            
            fontCache[fontKey] = result;
            return result;
        }

        private Texture GetImageForImageElement(ImageElement element)
        {
            Texture result;

            if (!string.IsNullOrEmpty(element.Filename))
            {
                if (imageCache.TryGetValue(element, out result)) return result;
                
                result = ToDispose(Texture.FromFile(Device, element.Filename));

                imageCache[element] = result;
            }
            else if (!imageCache.TryGetValue(element, out result) && element.Bitmap != null)
            {
                using (var ms = new MemoryStream())
                {
                    element.Bitmap.Save(ms, ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    result = ToDispose(Texture.FromStream(Device, ms));
                }

                imageCache[element] = result;
            }
            return result;
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">true if disposing both unmanaged and managed</param>
        protected override void Dispose(bool disposing)
        {
            if (true)
            {
                Device = null;
            }
        }
    }
}
