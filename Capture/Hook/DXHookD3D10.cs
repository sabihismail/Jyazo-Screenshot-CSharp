using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Capture.Interface;
using SharpDX;
using SharpDX.Direct3D10;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D10.Device; //using SlimDX.DXGI;
using Rectangle = System.Drawing.Rectangle;
using Resource = SharpDX.Direct3D10.Resource;
// ReSharper disable UnusedMember.Global

//using SlimDX.Direct3D10;
//using SlimDX;
//using Device = SlimDX.Direct3D10.Device;

namespace Capture.Hook
{
    // ReSharper disable once UnusedType.Global
    internal enum D3D10DeviceVTbl : short
    {
        // IUnknown
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,

        // ID3D10Device
        VS_SET_CONSTANT_BUFFERS = 3,
        PS_SET_SHADER_RESOURCES = 4,
        PS_SET_SHADER = 5,
        PS_SET_SAMPLERS = 6,
        VS_SET_SHADER = 7,
        DrawIndexed = 8,
        Draw = 9,
        PS_SET_CONSTANT_BUFFERS = 10,
        IA_SET_INPUT_LAYOUT = 11,
        IA_SET_VERTEX_BUFFERS = 12,
        IA_SET_INDEX_BUFFER = 13,
        DrawIndexedInstanced = 14,
        DrawInstanced = 15,
        GS_SET_CONSTANT_BUFFERS = 16,
        GS_SET_SHADER = 17,
        IA_SET_PRIMITIVE_TOPOLOGY = 18,
        VS_SET_SHADER_RESOURCES = 19,
        VS_SET_SAMPLERS = 20,
        SetPredication = 21,
        GS_SET_SHADER_RESOURCES = 22,
        GS_SET_SAMPLERS = 23,
        OM_SET_RENDER_TARGETS = 24,
        OM_SET_BLEND_STATE = 25,
        OM_SET_DEPTH_STENCIL_STATE = 26,
        SO_SET_TARGETS = 27,
        DrawAuto = 28,
        RS_SET_STATE = 29,
        RS_SET_VIEWPORTS = 30,
        RS_SET_SCISSOR_RECTS = 31,
        CopySubresourceRegion = 32,
        CopyResource = 33,
        UpdateSubresource = 34,
        ClearRenderTargetView = 35,
        ClearDepthStencilView = 36,
        GenerateMips = 37,
        ResolveSubresource = 38,
        VS_GET_CONSTANT_BUFFERS = 39,
        PS_GET_SHADER_RESOURCES = 40,
        PS_GET_SHADER = 41,
        PS_GET_SAMPLERS = 42,
        VS_GET_SHADER = 43,
        PS_GET_CONSTANT_BUFFERS = 44,
        IA_GET_INPUT_LAYOUT = 45,
        IA_GET_VERTEX_BUFFERS = 46,
        IA_GET_INDEX_BUFFER = 47,
        GS_GET_CONSTANT_BUFFERS = 48,
        GS_GET_SHADER = 49,
        IA_GET_PRIMITIVE_TOPOLOGY = 50,
        VS_GET_SHADER_RESOURCES = 51,
        VS_GET_SAMPLERS = 52,
        GetPredication = 53,
        GS_GET_SHADER_RESOURCES = 54,
        GS_GET_SAMPLERS = 55,
        OM_GET_RENDER_TARGETS = 56,
        OM_GET_BLEND_STATE = 57,
        OM_GET_DEPTH_STENCIL_STATE = 58,
        SO_GET_TARGETS = 59,
        RS_GET_STATE = 60,
        RS_GET_VIEWPORTS = 61,
        RS_GET_SCISSOR_RECTS = 62,
        GetDeviceRemovedReason = 63,
        SetExceptionMode = 64,
        GetExceptionMode = 65,
        GetPrivateData = 66,
        SetPrivateData = 67,
        SetPrivateDataInterface = 68,
        ClearState = 69,
        Flush = 70,
        CreateBuffer = 71,
        CreateTexture1D = 72,
        CreateTexture2D = 73,
        CreateTexture3D = 74,
        CreateShaderResourceView = 75,
        CreateRenderTargetView = 76,
        CreateDepthStencilView = 77,
        CreateInputLayout = 78,
        CreateVertexShader = 79,
        CreateGeometryShader = 80,
        CreateGeometryShaderWithStreamOutput = 81,
        CreatePixelShader = 82,
        CreateBlendState = 83,
        CreateDepthStencilState = 84,
        CreateRasterizerState = 85,
        CreateSamplerState = 86,
        CreateQuery = 87,
        CreatePredicate = 88,
        CreateCounter = 89,
        CheckFormatSupport = 90,
        CheckMultisampleQualityLevels = 91,
        CheckCounterInfo = 92,
        CheckCounter = 93,
        GetCreationFlags = 94,
        OpenSharedResource = 95,
        SetTextFilterSize = 96,
        GetTextFilterSize = 97
    }

    /// <summary>
    /// Direct3D 10 Hook - this hooks the SwapChain.Present method to capture images
    /// </summary>
    internal class DXHookD3D10: BaseDXHook
    {
        private const int D_3D10_DEVICE_METHOD_COUNT = 98;

        public DXHookD3D10(CaptureInterface ssInterface)
            : base(ssInterface)
        {
            DebugMessage("Create");
        }

        private List<IntPtr> d3d10VTblAddresses;
        private List<IntPtr> dxgiSwapChainVTblAddresses;

        private Hook<DxgiSwapChainPresentDelegate> dxgiSwapChainPresentHook;
        private Hook<DxgiSwapChainResizeTargetDelegate> dxgiSwapChainResizeTargetHook;

        protected override string HookName => "DXHookD3D10";

        public override void Hook()
        {
            DebugMessage("Hook: Begin");

            // Determine method addresses in Direct3D10.Device, and DXGI.SwapChain
            if (d3d10VTblAddresses == null)
            {
                d3d10VTblAddresses = new List<IntPtr>();
                dxgiSwapChainVTblAddresses = new List<IntPtr>();
                DebugMessage("Hook: Before device creation");
                using var factory = new Factory1();
                using var device = new Device(factory.GetAdapter(0), DeviceCreationFlags.None);
                
                DebugMessage("Hook: Device created");
                d3d10VTblAddresses.AddRange(GetVTblAddresses(device.NativePointer, D_3D10_DEVICE_METHOD_COUNT));

                using var renderForm = new Form();
                using var sc = new SwapChain(factory, device, Dxgi.CreateSwapChainDescription(renderForm.Handle));
                
                dxgiSwapChainVTblAddresses.AddRange(GetVTblAddresses(sc.NativePointer, Dxgi.DXGI_SWAPCHAIN_METHOD_COUNT));
            }

            // We will capture the backbuffer here
            dxgiSwapChainPresentHook = new Hook<DxgiSwapChainPresentDelegate>(
                dxgiSwapChainVTblAddresses[(int)Dxgi.DxgiSwapChainVTbl.Present],
                new DxgiSwapChainPresentDelegate(PresentHook),
                this);

            // We will capture target/window resizes here
            dxgiSwapChainResizeTargetHook = new Hook<DxgiSwapChainResizeTargetDelegate>(
                dxgiSwapChainVTblAddresses[(int)Dxgi.DxgiSwapChainVTbl.ResizeTarget],
                new DxgiSwapChainResizeTargetDelegate(ResizeTargetHook),
                this);

            /*
             * Don't forget that all hooks will start deactivated...
             * The following ensures that all threads are intercepted:
             * Note: you must do this for each hook.
             */
            dxgiSwapChainPresentHook.Activate();

            dxgiSwapChainResizeTargetHook.Activate();

            Hooks.Add(dxgiSwapChainPresentHook);
            Hooks.Add(dxgiSwapChainResizeTargetHook);
        }

        public override void Cleanup()
        {
        }

        /// <summary>
        /// The IDXGISwapChain.Present function definition
        /// </summary>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int DxgiSwapChainPresentDelegate(IntPtr swapChainPtr, int syncInterval, PresentFlags flags);

        /// <summary>
        /// The IDXGISwapChain.ResizeTarget function definition
        /// </summary>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int DxgiSwapChainResizeTargetDelegate(IntPtr swapChainPtr, ref ModeDescription newTargetParameters);

        /// <summary>
        /// Hooked to allow resizing a texture/surface that is reused. Currently not in use as we create the texture for each request
        /// to support different sizes each time (as we use DirectX to copy only the region we are after rather than the entire backbuffer)
        /// </summary>
        /// <param name="swapChainPtr"></param>
        /// <param name="newTargetParameters"></param>
        /// <returns></returns>
        private static int ResizeTargetHook(IntPtr swapChainPtr, ref ModeDescription newTargetParameters)
        {
            var swapChain = (SwapChain)swapChainPtr;

            // This version creates a new texture for each request so there is nothing to resize.
            // IF the size of the texture is known each time, we could create it once, and then possibly need to resize it here

            swapChain.ResizeTarget(ref newTargetParameters);
            return Result.Ok.Code;
        }

        /// <summary>
        /// Our present hook that will grab a copy of the backbuffer when requested. Note: this supports multi-sampling (anti-aliasing)
        /// </summary>
        /// <param name="swapChainPtr"></param>
        /// <param name="syncInterval"></param>
        /// <param name="flags"></param>
        /// <returns>The HRESULT of the original method</returns>
        private int PresentHook(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {
            Frame();
            var swapChain = (SwapChain)swapChainPtr;

            try
            {
                #region Screenshot Request
                if (Request != null)
                {
                    try
                    {
                        DebugMessage("PresentHook: Request Start");
                        var startTime = DateTime.Now;
                        using (var texture = Resource.FromSwapChain<Texture2D>(swapChain, 0))
                        {
                            #region Determine region to capture
                            var regionToCapture = new Rectangle(0, 0, texture.Description.Width, texture.Description.Height);

                            if (Request.RegionToCapture.Width > 0)
                            {
                                regionToCapture = Request.RegionToCapture;
                            }
                            #endregion

                            var theTexture = texture;

                            // If texture is multisampled, then we can use ResolveSubresource to copy it into a non-multisampled texture
                            Texture2D textureResolved = null;
                            if (texture.Description.SampleDescription.Count > 1)
                            {
                                DebugMessage("PresentHook: resolving multi-sampled texture");
                                // texture is multi-sampled, lets resolve it down to single sample
                                textureResolved = new Texture2D(texture.Device, new Texture2DDescription
                                {
                                    CpuAccessFlags = CpuAccessFlags.None,
                                    Format = texture.Description.Format,
                                    Height = texture.Description.Height,
                                    Usage = ResourceUsage.Default,
                                    Width = texture.Description.Width,
                                    ArraySize = 1,
                                    SampleDescription = new SampleDescription(1, 0), // Ensure single sample
                                    BindFlags = BindFlags.None,
                                    MipLevels = 1,
                                    OptionFlags = texture.Description.OptionFlags
                                });
                                // Resolve into textureResolved
                                texture.Device.ResolveSubresource(texture, 0, textureResolved, 0, texture.Description.Format);

                                // Make "theTexture" be the resolved texture
                                theTexture = textureResolved;
                            }

                            // Create destination texture
                            var textureDest = new Texture2D(texture.Device, new Texture2DDescription
                            {
                                    CpuAccessFlags = CpuAccessFlags.None,// CpuAccessFlags.Write | CpuAccessFlags.Read,
                                    Format = Format.R8G8B8A8_UNorm, // Supports BMP/PNG
                                    Height = regionToCapture.Height,
                                    Usage = ResourceUsage.Default,// ResourceUsage.Staging,
                                    Width = regionToCapture.Width,
                                    ArraySize = 1,//texture.Description.ArraySize,
                                    SampleDescription = new SampleDescription(1, 0),// texture.Description.SampleDescription,
                                    BindFlags = BindFlags.None,
                                    MipLevels = 1,//texture.Description.MipLevels,
                                    OptionFlags = texture.Description.OptionFlags
                                });

                            // Copy the subresource region, we are dealing with a flat 2D texture with no MipMapping, so 0 is the subresource index
                            theTexture.Device.CopySubresourceRegion(theTexture, 0, new ResourceRegion
                            {
                                Top = regionToCapture.Top,
                                Bottom = regionToCapture.Bottom,
                                Left = regionToCapture.Left,
                                Right = regionToCapture.Right,
                                Front = 0,
                                Back = 1 // Must be 1 or only black will be copied
                            }, textureDest, 0, 0, 0, 0);

                            // Note: it would be possible to capture multiple frames and process them in a background thread

                            // Copy to memory and send back to host process on a background thread so that we do not cause any delay in the rendering pipeline
                            var request = Request.Clone(); // this.Request gets set to null, so copy the Request for use in the thread
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                //FileStream fs = new FileStream(@"c:\temp\temp.bmp", FileMode.Create);
                                //Texture2D.ToStream(testSubResourceCopy, ImageFileFormat.Bmp, fs);

                                var startCopyToSystemMemory = DateTime.Now;
                                using (var ms = new MemoryStream())
                                {
                                    Resource.ToStream(textureDest, ImageFileFormat.Bmp, ms);
                                    ms.Position = 0;
                                    DebugMessage("PresentHook: Copy to System Memory time: " + (DateTime.Now - startCopyToSystemMemory));

                                    var startSendResponse = DateTime.Now;
                                    ProcessCapture(ms, request);
                                    DebugMessage("PresentHook: Send response time: " + (DateTime.Now - startSendResponse));
                                }

                                // Free the textureDest as we no longer need it.
                                textureDest.Dispose();
                                textureDest = null;
                                DebugMessage("PresentHook: Full Capture time: " + (DateTime.Now - startTime));
                            });
                            
                            // Make sure we free up the resolved texture if it was created
                            textureResolved?.Dispose();
                        }

                        DebugMessage("PresentHook: Copy BackBuffer time: " + (DateTime.Now - startTime));
                        DebugMessage("PresentHook: Request End");
                    }
                    finally
                    {
                        // Prevent the request from being processed a second time
                        Request = null;
                    }

                }
                #endregion

                #region Example: Draw overlay (after screenshot so we don't capture overlay as well)
                if (Config.ShowOverlay)
                {
                    using var texture = Resource.FromSwapChain<Texture2D>(swapChain, 0);
                    
                    if (Fps.GetFps() >= 1)
                    {
                        var fd = new FontDescription
                        {
                            Height = 16,
                            FaceName = "Arial",
                            Italic = false,
                            Width = 0,
                            MipLevels = 1,
                            CharacterSet = FontCharacterSet.Default,
                            OutputPrecision = FontPrecision.Default,
                            Quality = FontQuality.Antialiased,
                            PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.DontCare,
                            Weight = FontWeight.Bold
                        };

                        // TODO: Font should not be created every frame!
                        using var font = new Font(texture.Device, fd);
                        DrawText(font, new Vector2(5, 5), $"{Fps.GetFps():N0} fps", Color.Red);

                        if (TextDisplay is { Display: true })
                        {
                            DrawText(font, new Vector2(5, 25), TextDisplay.Text, new Color4(Color.Red.R, Color.Red.G, Color.Red.B, 
                                Math.Abs(1.0f - TextDisplay.Remaining)));
                        }
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                // If there is an error we do not want to crash the hooked application, so swallow the exception
                DebugMessage("PresentHook: Exception: " + e.GetType().FullName + ": " + e.Message);
            }

            // As always we need to call the original method, note that EasyHook has already re-patched the original method
            // so calling it here will not cause an endless recursion to this function
            swapChain.Present(syncInterval, flags);
            return Result.Ok.Code;
        }

        private static void DrawText(Font font, Vector2 pos, string text, Color4 color)
        {
            font.DrawText(null, text, new SharpDX.Rectangle((int)pos.X, (int)pos.Y, 0, 0), FontDrawFlags.NoClip, color);
        }
    }
}
