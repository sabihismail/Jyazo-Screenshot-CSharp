using System;
using System.Collections.Generic;
using System.Linq;
using Capture.Hook.Common;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace Capture.Hook.DX11
{
    internal class DXOverlayEngine: Component
    {
        public List<IOverlay> Overlays { get; private set; }

        private bool DeferredContext => deviceContext.TypeInfo == DeviceContextType.Deferred;

        private bool initialised;
        private bool initialising;

        private Device device;
        private DeviceContext deviceContext;
        private Texture2D renderTarget;
        private RenderTargetView renderTargetView;
        private DXSprite spriteEngine;
        private readonly Dictionary<string, DXFont> fontCache = new();
        private readonly Dictionary<Element, DXImage> imageCache = new();

        public DXOverlayEngine()
        {
            Overlays = new List<IOverlay>();
        }

        private void EnsureInitialized()
        {
            if (!initialised)
                throw new InvalidOperationException("DXOverlayEngine must be initialised.");
        }

        public bool Initialise(SwapChain swapChain)
        {
            return Initialise(swapChain.GetDevice<Device>(), swapChain.GetBackBuffer<Texture2D>(0));
        }

        private bool Initialise(Device deviceIn, Texture2D renderTargetIn)
        {
            if (initialising)
                return false;

            initialising = true;
            
            try
            {

                device = deviceIn;
                renderTarget = renderTargetIn;
                try
                {
                    deviceContext = ToDispose(new DeviceContext(device));
                }
                catch (SharpDXException)
                {
                    deviceContext = device.ImmediateContext;
                }

                renderTargetView = ToDispose(new RenderTargetView(device, renderTarget));

                //if (DeferredContext)
                //{
                //    ViewportF[] viewportF = { new ViewportF(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height, 0, 1) };
                //    _deviceContext.Rasterizer.SetViewports(viewportF);
                //    _deviceContext.OutputMerger.SetTargets(_renderTargetView);
                //}

                spriteEngine = new DXSprite(device, deviceContext);
                if (!spriteEngine.Initialize())
                    return false;

                // Initialise any resources required for overlay elements
                InitialiseElementResources();

                initialised = true;
                return true;
            }
            finally
            {
                initialising = false;
            }
        }

        private void InitialiseElementResources()
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
            //if (!DeferredContext)
            //{
                RawViewportF[] viewportF = { new ViewportF(0, 0, renderTarget.Description.Width, renderTarget.Description.Height, 0, 1) };
                deviceContext.Rasterizer.SetViewports(viewportF);
                deviceContext.OutputMerger.SetTargets(renderTargetView);
            //}
        }

        /// <summary>
        /// Draw the overlay(s)
        /// </summary>
        public void Draw()
        {
            EnsureInitialized();

            Begin();

            foreach (var element in Overlays.Where(overlay => !overlay.Hidden)
                .SelectMany(overlay => overlay.Elements.Where(element => !element.Hidden)))
            {
                switch (element)
                {
                    case TextElement textElement:
                    {
                        var font = GetFontForTextElement(textElement);
                        if (font != null && !string.IsNullOrEmpty(textElement.Text))
                            spriteEngine.DrawString(textElement.Location.X, textElement.Location.Y, textElement.Text, textElement.Color, font);
                        break;
                    }
                    case ImageElement imageElement:
                    {
                        var image = GetImageForImageElement(imageElement);
                        if (image != null)
                            spriteEngine.DrawImage(imageElement.Location.X, imageElement.Location.Y, imageElement.Scale, imageElement.Angle, imageElement.Tint, image);
                        break;
                    }
                }
            }

            End();
        }

        private void End()
        {
            if (DeferredContext)
            {
                var commandList = deviceContext.FinishCommandList(true);
                device.ImmediateContext.ExecuteCommandList(commandList, true);
                commandList.Dispose();
            }
        }

        private DXFont GetFontForTextElement(TextElement element)
        {
            var fontKey = $"{element.Font.Name}{element.Font.Size}{element.Font.Style}{element.AntiAliased}";

            if (fontCache.TryGetValue(fontKey, out var result)) return result;
            
            result = ToDispose(new DXFont(device, deviceContext));
            result.Initialize(element.Font.Name, element.Font.Size, element.Font.Style, element.AntiAliased);
            fontCache[fontKey] = result;
            
            return result;
        }

        private DXImage GetImageForImageElement(ImageElement element)
        {
            if (imageCache.TryGetValue(element, out var result)) return result;
            
            result = ToDispose(new DXImage(device, deviceContext));
            result.Initialise(element.Bitmap);
            imageCache[element] = result;

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
                device = null;
            }
        }
    }
}
