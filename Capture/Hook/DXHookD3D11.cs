using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Capture.Hook.DX11;
using Capture.Interface;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.WIC;
using SharpDX.Windows;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Resource = SharpDX.DXGI.Resource;
// ReSharper disable UnusedMember.Global

namespace Capture.Hook
{
    // ReSharper disable once UnusedType.Global
    internal enum D3D11DeviceVTbl : short
    {
        // IUnknown
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,

        // ID3D11Device
        CreateBuffer = 3,
        CreateTexture1D = 4,
        CreateTexture2D = 5,
        CreateTexture3D = 6,
        CreateShaderResourceView = 7,
        CreateUnorderedAccessView = 8,
        CreateRenderTargetView = 9,
        CreateDepthStencilView = 10,
        CreateInputLayout = 11,
        CreateVertexShader = 12,
        CreateGeometryShader = 13,
        CreateGeometryShaderWithStreamOutput = 14,
        CreatePixelShader = 15,
        CreateHullShader = 16,
        CreateDomainShader = 17,
        CreateComputeShader = 18,
        CreateClassLinkage = 19,
        CreateBlendState = 20,
        CreateDepthStencilState = 21,
        CreateRasterizerState = 22,
        CreateSamplerState = 23,
        CreateQuery = 24,
        CreatePredicate = 25,
        CreateCounter = 26,
        CreateDeferredContext = 27,
        OpenSharedResource = 28,
        CheckFormatSupport = 29,
        CheckMultisampleQualityLevels = 30,
        CheckCounterInfo = 31,
        CheckCounter = 32,
        CheckFeatureSupport = 33,
        GetPrivateData = 34,
        SetPrivateData = 35,
        SetPrivateDataInterface = 36,
        GetFeatureLevel = 37,
        GetCreationFlags = 38,
        GetDeviceRemovedReason = 39,
        GetImmediateContext = 40,
        SetExceptionMode = 41,
        GetExceptionMode = 42
    }

    /// <summary>
    /// Direct3D 11 Hook - this hooks the SwapChain.Present to take screenshots
    /// </summary>
    internal class DXHookD3D11: BaseDXHook
    {
        private const int D_3D11_DEVICE_METHOD_COUNT = 43;

        public DXHookD3D11(CaptureInterface ssInterface)
            : base(ssInterface)
        {
        }

        private List<IntPtr> d3d11VTblAddresses;
        private List<IntPtr> dxgiSwapChainVTblAddresses;

        private Hook<DxgiSwapChainPresentDelegate> dxgiSwapChainPresentHook;
        private Hook<DxgiSwapChainResizeTargetDelegate> dxgiSwapChainResizeTargetHook;

        private readonly object mutex = new();

        #region Internal device resources

        private Device device;
        private SwapChain swapChain;
        private RenderForm renderForm;
        private Texture2D resolvedRtShared;
#pragma warning disable 169
        private KeyedMutex resolvedRtSharedKeyedMutex;
#pragma warning restore 169
        private ShaderResourceView resolvedSrv;
        private ScreenAlignedQuadRenderer saQuad;
        private Texture2D finalRt;
        private Texture2D resizedRt;
        private RenderTargetView resizedRtv;
        #endregion

        private Query query;
#pragma warning disable 414
        private bool queryIssued;
#pragma warning restore 414
        private bool finalRtMapped;
        private ScreenshotRequest requestCopy;

        #region Main device resources

        private Texture2D resolvedRt;
        private KeyedMutex resolvedRtKeyedMutex;
        private KeyedMutex resolvedRtKeyedMutexDev2;
        #endregion

        protected override string HookName => "DXHookD3D11";

        public override void Hook()
        {
            DebugMessage("Hook: Begin");
            if (d3d11VTblAddresses == null)
            {
                d3d11VTblAddresses = new List<IntPtr>();
                dxgiSwapChainVTblAddresses = new List<IntPtr>();

                #region Get Device and SwapChain method addresses
                // Create temporary device + swapchain and determine method addresses
                renderForm = ToDispose(new RenderForm());
                DebugMessage("Hook: Before device creation");
                Device.CreateWithSwapChain(
                    DriverType.Hardware,
                    DeviceCreationFlags.BgraSupport,
                    Dxgi.CreateSwapChainDescription(renderForm.Handle),
                    out device,
                    out swapChain);

                ToDispose(device);
                ToDispose(swapChain);

                if (device != null && swapChain != null)
                {
                    DebugMessage("Hook: Device created");
                    d3d11VTblAddresses.AddRange(GetVTblAddresses(device.NativePointer, D_3D11_DEVICE_METHOD_COUNT));
                    dxgiSwapChainVTblAddresses.AddRange(GetVTblAddresses(swapChain.NativePointer, Dxgi.DXGI_SWAPCHAIN_METHOD_COUNT));
                }
                else
                {
                    DebugMessage("Hook: Device creation failed");
                }
                #endregion
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
            try
            {
                if (overlayEngine == null) return;
                
                overlayEngine.Dispose();
                overlayEngine = null;
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// The IDXGISwapChain.Present function definition
        /// </summary>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int DxgiSwapChainPresentDelegate(IntPtr swapChainPtr, int syncInterval, /* int */ PresentFlags flags);

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
        private int ResizeTargetHook(IntPtr swapChainPtr, ref ModeDescription newTargetParameters)
        {
            // Dispose of overlay engine (so it will be recreated with correct renderTarget view size)
            if (overlayEngine == null) return dxgiSwapChainResizeTargetHook.Original(swapChainPtr, ref newTargetParameters);
            
            overlayEngine.Dispose();
            overlayEngine = null;

            return dxgiSwapChainResizeTargetHook.Original(swapChainPtr, ref newTargetParameters);
        }

        private void EnsureResources(Device deviceIn, Texture2DDescription description, Rectangle captureRegion, ScreenshotRequest request, bool useSameDeviceForResize = false)
        {
            var resizeDevice = useSameDeviceForResize ? deviceIn : device;

            // Check if _resolvedRT or _finalRT require creation
            if (finalRt != null && (finalRt.Device.NativePointer == deviceIn.NativePointer || finalRt.Device.NativePointer == device.NativePointer) &&
                finalRt.Description.Height == captureRegion.Height && finalRt.Description.Width == captureRegion.Width &&
                resolvedRt != null && resolvedRt.Description.Height == description.Height && resolvedRt.Description.Width == description.Width &&
                (resolvedRt.Device.NativePointer == deviceIn.NativePointer || resolvedRt.Device.NativePointer == device.NativePointer) && resolvedRt.Description.Format == description.Format
                )
            {

            }
            else
            {
                RemoveAndDispose(ref query);
                RemoveAndDispose(ref resolvedRt);
                RemoveAndDispose(ref resolvedSrv);
                RemoveAndDispose(ref finalRt);
                RemoveAndDispose(ref resolvedRtShared);
                RemoveAndDispose(ref resolvedRtKeyedMutex);
                RemoveAndDispose(ref resolvedRtKeyedMutexDev2);

                query = new Query(resizeDevice, new QueryDescription
                {
                    Flags = QueryFlags.None,
                    Type = QueryType.Event
                });
                queryIssued = false;

                try
                {
                    var resolvedRtOptionFlags = ResourceOptionFlags.None;

                    if (deviceIn != resizeDevice)
                        resolvedRtOptionFlags |= ResourceOptionFlags.SharedKeyedmutex;

                    resolvedRt = ToDispose(new Texture2D(deviceIn, new Texture2DDescription
                    {
                        CpuAccessFlags = CpuAccessFlags.None,
                        Format = description.Format, // for multisampled backbuffer, this must be same format
                        Height = description.Height,
                        Usage = ResourceUsage.Default,
                        Width = description.Width,
                        ArraySize = 1,
                        SampleDescription = new SampleDescription(1, 0), // Ensure single sample
                        BindFlags = BindFlags.ShaderResource,
                        MipLevels = 1,
                        OptionFlags = resolvedRtOptionFlags
                    }));
                }
                catch
                {
                    // Failed to create the shared resource, try again using the same device as game for resize
                    EnsureResources(deviceIn, description, captureRegion, request, true);
                    return;
                }

                // Retrieve reference to the keyed mutex
                resolvedRtKeyedMutex = ToDispose(resolvedRt.QueryInterfaceOrNull<KeyedMutex>());

                // If the resolvedRT is a shared resource _resolvedRTKeyedMutex will not be null
                if (resolvedRtKeyedMutex != null)
                {
                    using (var resource = resolvedRt.QueryInterface<Resource>())
                    {
                        resolvedRtShared = ToDispose(resizeDevice.OpenSharedResource<Texture2D>(resource.SharedHandle));
                        resolvedRtKeyedMutexDev2 = ToDispose(resolvedRtShared.QueryInterfaceOrNull<KeyedMutex>());
                    }
                    // SRV for use if resizing
                    resolvedSrv = ToDispose(new ShaderResourceView(resizeDevice, resolvedRtShared));
                }
                else
                {
                    resolvedSrv = ToDispose(new ShaderResourceView(resizeDevice, resolvedRt));
                }

                finalRt = ToDispose(new Texture2D(resizeDevice, new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    Format = description.Format,
                    Height = captureRegion.Height,
                    Usage = ResourceUsage.Staging,
                    Width = captureRegion.Width,
                    ArraySize = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    BindFlags = BindFlags.None,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None
                }));
                finalRtMapped = false;
            }

            if (resolvedRt != null && resolvedRtKeyedMutexDev2 == null && resizeDevice == device)
                resizeDevice = deviceIn;

            if (resizeDevice == null || request.Resize == null || resizedRt != null 
                && resizedRt.Device.NativePointer == resizeDevice.NativePointer && resizedRt.Description.Width == request.Resize.Value.Width 
                && resizedRt.Description.Height == request.Resize.Value.Height) return;
            
            // Create/Recreate resources for resizing
            RemoveAndDispose(ref resizedRt);
            RemoveAndDispose(ref resizedRtv);
            RemoveAndDispose(ref saQuad);

            resizedRt = ToDispose(new Texture2D(resizeDevice, new Texture2DDescription
            {
                Format = Format.R8G8B8A8_UNorm, // Supports BMP/PNG/etc
                Height = request.Resize.Value.Height,
                Width = request.Resize.Value.Width,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags = BindFlags.RenderTarget,
                MipLevels = 1,
                Usage = ResourceUsage.Default,
                OptionFlags = ResourceOptionFlags.None
            }));

            resizedRtv = ToDispose(new RenderTargetView(resizeDevice, resizedRt));

            saQuad = ToDispose(new ScreenAlignedQuadRenderer());
            saQuad.Initialize(new DeviceManager(resizeDevice));
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
            var swapChainIn = (SwapChain)swapChainPtr;
            try
            {
                #region Screenshot Request
                if (Request != null)
                {
                    DebugMessage("PresentHook: Request Start");
                    var startTime = DateTime.Now;
                    using (var currentRt = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChainIn, 0))
                    {
                        #region Determine region to capture
                        var captureRegion = new Rectangle(0, 0, currentRt.Description.Width, currentRt.Description.Height);

                        if (Request.RegionToCapture.Width > 0)
                        {
                            captureRegion = new Rectangle(Request.RegionToCapture.Left, Request.RegionToCapture.Top, Request.RegionToCapture.Right, 
                                Request.RegionToCapture.Bottom);
                        }
                        else if (Request.Resize.HasValue)
                        {
                            captureRegion = new Rectangle(0, 0, Request.Resize.Value.Width, Request.Resize.Value.Height);
                        }
                        #endregion

                        // Create / Recreate resources as necessary
                        EnsureResources(currentRt.Device, currentRt.Description, captureRegion, Request);

                        Texture2D sourceTexture;

                        // If texture is multisampled, then we can use ResolveSubresource to copy it into a non-multisampled texture
                        if (currentRt.Description.SampleDescription.Count > 1 || Request.Resize.HasValue)
                        {
                            DebugMessage(Request.Resize.HasValue ? "PresentHook: resizing texture" : "PresentHook: resolving multi-sampled texture");

                            // Resolve into _resolvedRT
                            resolvedRtKeyedMutex?.Acquire(0, int.MaxValue);
                            currentRt.Device.ImmediateContext.ResolveSubresource(currentRt, 0, resolvedRt, 0,
                                resolvedRt.Description.Format);
                            resolvedRtKeyedMutex?.Release(1);

                            if (Request.Resize.HasValue)
                            {
                                lock(mutex)
                                {
                                    resolvedRtKeyedMutexDev2?.Acquire(1, int.MaxValue);
                                    saQuad.ShaderResource = resolvedSrv;
                                    saQuad.RenderTargetView = resizedRtv;
                                    saQuad.RenderTarget = resizedRt;
                                    saQuad.Render();
                                    resolvedRtKeyedMutexDev2?.Release(0);
                                }

                                // set sourceTexture to the resized RT
                                sourceTexture = resizedRt;
                            }
                            else
                            {
                                // Make sourceTexture be the resolved texture
                                sourceTexture = resolvedRtShared ?? resolvedRt;
                            }
                        }
                        else
                        {
                            // Copy the resource into the shared texture
                            resolvedRtKeyedMutex?.Acquire(0, int.MaxValue);
                            currentRt.Device.ImmediateContext.CopySubresourceRegion(currentRt, 0, null, resolvedRt, 0);
                            resolvedRtKeyedMutex?.Release(1);

                            sourceTexture = resolvedRtShared ?? resolvedRt;
                        }

                        // Copy to memory and send back to host process on a background thread so that we do not cause any delay in the rendering pipeline
                        requestCopy = Request.Clone(); // this.Request gets set to null, so copy the Request for use in the thread

                        // Prevent the request from being processed a second time
                        Request = null;

                        var acquireLock = sourceTexture == resolvedRtShared;
                        
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            // Acquire lock on second device
                            if (acquireLock && resolvedRtKeyedMutexDev2 != null)
                                resolvedRtKeyedMutexDev2.Acquire(1, int.MaxValue);

                            lock (mutex)
                            {
                                // Copy the subresource region, we are dealing with a flat 2D texture with no MipMapping, so 0 is the subresource index
                                sourceTexture.Device.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, new ResourceRegion
                                {
                                    Top = captureRegion.Top,
                                    Bottom = captureRegion.Bottom,
                                    Left = captureRegion.Left,
                                    Right = captureRegion.Right,
                                    Front = 0,
                                    Back = 1 // Must be 1 or only black will be copied
                                }, finalRt, 0);

                                // Release lock upon shared surface on second device
                                if (acquireLock && resolvedRtKeyedMutexDev2 != null)
                                    resolvedRtKeyedMutexDev2.Release(0);

                                finalRt.Device.ImmediateContext.End(query);
                                queryIssued = true;
                                while (finalRt.Device.ImmediateContext.GetData(query).ReadByte() != 1)
                                {
                                    // Spin (usually only one cycle or no spin takes place)
                                }

                                var startCopyToSystemMemory = DateTime.Now;
                                try
                                {
                                    var db = default(DataBox);
                                    if (requestCopy.Format == ImageFormat.PixelData)
                                    {
                                        db = finalRt.Device.ImmediateContext.MapSubresource(finalRt, 0, MapMode.Read, MapFlags.DoNotWait);
                                        finalRtMapped = true;
                                    }
                                    queryIssued = false;

                                    try
                                    {
                                        using var ms = new MemoryStream();
                                        switch (requestCopy.Format)
                                        {
                                            case ImageFormat.Bitmap:
                                            case ImageFormat.Jpeg:
                                            case ImageFormat.Png:
                                                ToStream(finalRt.Device.ImmediateContext, finalRt, requestCopy.Format, ms);
                                                break;
                                            
                                            case ImageFormat.PixelData:
                                                if (db.DataPointer != IntPtr.Zero)
                                                {
                                                    ProcessCapture(finalRt.Description.Width, finalRt.Description.Height, db.RowPitch, PixelFormat.Format32bppArgb, db.DataPointer, 
                                                        requestCopy);
                                                }
                                                return;
                                            
                                            default:
                                                throw new ArgumentOutOfRangeException();
                                        }
                                        ms.Position = 0;
                                        ProcessCapture(ms, requestCopy);
                                    }
                                    finally
                                    {
                                        DebugMessage("PresentHook: Copy to System Memory time: " + (DateTime.Now - startCopyToSystemMemory));
                                        
                                        if (finalRtMapped)
                                        {
                                            lock (mutex)
                                            {
                                                finalRt.Device.ImmediateContext.UnmapSubresource(finalRt, 0);
                                                finalRtMapped = false;
                                            }
                                        }
                                    }
                                }
                                catch (SharpDXException)
                                {
                                    // Catch DXGI_ERROR_WAS_STILL_DRAWING and ignore - the data isn't available yet
                                }
                            }
                        });
                        
                        // Note: it would be possible to capture multiple frames and process them in a background thread
                    }
                    DebugMessage("PresentHook: Copy BackBuffer time: " + (DateTime.Now - startTime));
                    DebugMessage("PresentHook: Request End");
                }
                #endregion

                #region Draw overlay (after screenshot so we don't capture overlay as well)
                var displayOverlays = Overlays;
                if (Config.ShowOverlay && displayOverlays != null)
                {
                    // Initialise Overlay Engine
                    if (swapChainPointer != swapChainIn.NativePointer || overlayEngine == null || IsOverlayUpdatePending)
                    {
                        overlayEngine?.Dispose();

                        overlayEngine = new DXOverlayEngine();
                        overlayEngine.Overlays.AddRange(displayOverlays);
                        overlayEngine.Initialise(swapChainIn);

                        swapChainPointer = swapChainIn.NativePointer;

                        IsOverlayUpdatePending = false;
                    }
                    // Draw Overlay(s)
                    if (overlayEngine != null)
                    {
                        foreach (var overlay in overlayEngine.Overlays)
                            overlay.Frame();
                        overlayEngine.Draw();
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                // If there is an error we do not want to crash the hooked application, so swallow the exception
                DebugMessage("PresentHook: Exception: " + e.GetType().FullName + ": " + e);
                //return unchecked((int)0x8000FFFF); //E_UNEXPECTED
            }

            // As always we need to call the original method, note that EasyHook will automatically skip the hook and call the original method
            // i.e. calling it here will not cause a stack overflow into this function
            return dxgiSwapChainPresentHook.Original(swapChainPtr, syncInterval, flags);
        }

        private DXOverlayEngine overlayEngine;

        private IntPtr swapChainPointer = IntPtr.Zero;

        private ImagingFactory2 wicFactory;

        /// <summary>
        /// Copies to a stream using WIC. The format is converted if necessary.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="texture"></param>
        /// <param name="outputFormat"></param>
        /// <param name="stream"></param>
        private void ToStream(DeviceContext context, Texture2D texture, ImageFormat outputFormat, Stream stream)
        {
            wicFactory ??= ToDispose(new ImagingFactory2());

            var dataBox = context.MapSubresource(
                texture,
                0,
                0,
                MapMode.Read,
                MapFlags.None,
                out var dataStream);
            try
            {
                var dataRectangle = new DataRectangle
                {
                    DataPointer = dataStream.DataPointer,
                    Pitch = dataBox.RowPitch
                };

                var format = PixelFormatFromFormat(texture.Description.Format);

                if (format == Guid.Empty)
                    return;

                using var bitmap = new Bitmap(
                    wicFactory,
                    texture.Description.Width,
                    texture.Description.Height,
                    format,
                    dataRectangle);
                
                stream.Position = 0;

                BitmapEncoder bitmapEncoder = null;
                switch (outputFormat)
                {
                    case ImageFormat.Bitmap:
                        bitmapEncoder = new BmpBitmapEncoder(wicFactory, stream);
                        break;
                        
                    case ImageFormat.Jpeg:
                        bitmapEncoder = new JpegBitmapEncoder(wicFactory, stream);
                        break;
                        
                    case ImageFormat.Png:
                        bitmapEncoder = new PngBitmapEncoder(wicFactory, stream);
                        break;
                        
                    case ImageFormat.PixelData:
                        break;
                        
                    default:
                        return;
                }

                try
                {
                    using var bitmapFrameEncode = new BitmapFrameEncode(bitmapEncoder);
                    
                    bitmapFrameEncode.Initialize();
                    bitmapFrameEncode.SetSize(bitmap.Size.Width, bitmap.Size.Height);
                    var pixelFormat = format;
                    bitmapFrameEncode.SetPixelFormat(ref pixelFormat);

                    if (pixelFormat != format)
                    {
                        // IWICFormatConverter
                        using var converter = new FormatConverter(wicFactory);
                            
                        if (converter.CanConvert(format, pixelFormat))
                        {
                            converter.Initialize(bitmap, SharpDX.WIC.PixelFormat.Format24bppBGR, BitmapDitherType.None, null, 0, BitmapPaletteType.MedianCut);
                            bitmapFrameEncode.SetPixelFormat(ref pixelFormat);
                            bitmapFrameEncode.WriteSource(converter);
                        }
                        else
                        {
                            DebugMessage($"Unable to convert Direct3D texture format {texture.Description.Format.ToString()} to a suitable WIC format");
                            return;
                        }
                    }
                    else
                    {
                        bitmapFrameEncode.WriteSource(bitmap);
                    }
                    bitmapFrameEncode.Commit();
                    bitmapEncoder?.Commit();
                }
                finally
                {
                    bitmapEncoder?.Dispose();
                }
            }
            finally
            {
                context.UnmapSubresource(texture, 0);
            }
        }

        private static Guid PixelFormatFromFormat(Format format)
        {
            switch (format)
            {
                case Format.R32G32B32A32_Typeless:
                case Format.R32G32B32A32_Float:
                    return SharpDX.WIC.PixelFormat.Format128bppRGBAFloat;
                case Format.R32G32B32A32_UInt:
                case Format.R32G32B32A32_SInt:
                    return SharpDX.WIC.PixelFormat.Format128bppRGBAFixedPoint;
                case Format.R32G32B32_Typeless:
                case Format.R32G32B32_Float:
                    return SharpDX.WIC.PixelFormat.Format96bppRGBFloat;
                case Format.R32G32B32_UInt:
                case Format.R32G32B32_SInt:
                    return SharpDX.WIC.PixelFormat.Format96bppRGBFixedPoint;
                case Format.R16G16B16A16_Typeless:
                case Format.R16G16B16A16_Float:
                case Format.R16G16B16A16_UNorm:
                case Format.R16G16B16A16_UInt:
                case Format.R16G16B16A16_SNorm:
                case Format.R16G16B16A16_SInt:
                    return SharpDX.WIC.PixelFormat.Format64bppRGBA;
                case Format.R32G32_Typeless:
                case Format.R32G32_Float:
                case Format.R32G32_UInt:
                case Format.R32G32_SInt:
                case Format.R32G8X24_Typeless:
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                case Format.X32_Typeless_G8X24_UInt:
                    return Guid.Empty;
                case Format.R10G10B10A2_Typeless:
                case Format.R10G10B10A2_UNorm:
                case Format.R10G10B10A2_UInt:
                    return SharpDX.WIC.PixelFormat.Format32bppRGBA1010102;
                case Format.R11G11B10_Float:
                    return Guid.Empty;
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRgb:
                case Format.R8G8B8A8_UInt:
                case Format.R8G8B8A8_SNorm:
                case Format.R8G8B8A8_SInt:
                    return SharpDX.WIC.PixelFormat.Format32bppRGBA;
                case Format.R16G16_Typeless:
                case Format.R16G16_Float:
                case Format.R16G16_UNorm:
                case Format.R16G16_UInt:
                case Format.R16G16_SNorm:
                case Format.R16G16_SInt:
                    return Guid.Empty;
                case Format.R32_Typeless:
                case Format.D32_Float:
                case Format.R32_Float:
                case Format.R32_UInt:
                case Format.R32_SInt:
                    return Guid.Empty;
                case Format.R24G8_Typeless:
                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                    return SharpDX.WIC.PixelFormat.Format32bppGrayFloat;
                case Format.X24_Typeless_G8_UInt:
                case Format.R9G9B9E5_Sharedexp:
                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                    return Guid.Empty;
                case Format.B8G8R8A8_UNorm:
                case Format.B8G8R8X8_UNorm:
                    return SharpDX.WIC.PixelFormat.Format32bppBGRA;
                case Format.R10G10B10_Xr_Bias_A2_UNorm:
                    return SharpDX.WIC.PixelFormat.Format32bppBGR101010;
                case Format.B8G8R8A8_Typeless:
                case Format.B8G8R8A8_UNorm_SRgb:
                case Format.B8G8R8X8_Typeless:
                case Format.B8G8R8X8_UNorm_SRgb:
                    return SharpDX.WIC.PixelFormat.Format32bppBGRA;
                case Format.R8G8_Typeless:
                case Format.R8G8_UNorm:
                case Format.R8G8_UInt:
                case Format.R8G8_SNorm:
                case Format.R8G8_SInt:
                    return Guid.Empty;
                case Format.R16_Typeless:
                case Format.R16_Float:
                case Format.D16_UNorm:
                case Format.R16_UNorm:
                case Format.R16_SNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppGrayHalf;
                case Format.R16_UInt:
                case Format.R16_SInt:
                    return SharpDX.WIC.PixelFormat.Format16bppGrayFixedPoint;
                case Format.B5G6R5_UNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppBGR565;
                case Format.B5G5R5A1_UNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppBGRA5551;
                case Format.B4G4R4A4_UNorm:
                    return Guid.Empty;

                case Format.R8_Typeless:
                case Format.R8_UNorm:
                case Format.R8_UInt:
                case Format.R8_SNorm:
                case Format.R8_SInt:
                    return SharpDX.WIC.PixelFormat.Format8bppGray;
                case Format.A8_UNorm:
                    return SharpDX.WIC.PixelFormat.Format8bppAlpha;
                case Format.R1_UNorm:
                    return SharpDX.WIC.PixelFormat.Format1bppIndexed;

                case Format.Unknown:
                    break;
                case Format.BC1_Typeless:
                    break;
                case Format.BC1_UNorm:
                    break;
                case Format.BC1_UNorm_SRgb:
                    break;
                case Format.BC2_Typeless:
                    break;
                case Format.BC2_UNorm:
                    break;
                case Format.BC2_UNorm_SRgb:
                    break;
                case Format.BC3_Typeless:
                    break;
                case Format.BC3_UNorm:
                    break;
                case Format.BC3_UNorm_SRgb:
                    break;
                case Format.BC4_Typeless:
                    break;
                case Format.BC4_UNorm:
                    break;
                case Format.BC4_SNorm:
                    break;
                case Format.BC5_Typeless:
                    break;
                case Format.BC5_UNorm:
                    break;
                case Format.BC5_SNorm:
                    break;
                case Format.BC6H_Typeless:
                    break;
                case Format.BC6H_Uf16:
                    break;
                case Format.BC6H_Sf16:
                    break;
                case Format.BC7_Typeless:
                    break;
                case Format.BC7_UNorm:
                    break;
                case Format.BC7_UNorm_SRgb:
                    break;
                case Format.AYUV:
                    break;
                case Format.Y410:
                    break;
                case Format.Y416:
                    break;
                case Format.NV12:
                    break;
                case Format.P010:
                    break;
                case Format.P016:
                    break;
                case Format.Opaque420:
                    break;
                case Format.YUY2:
                    break;
                case Format.Y210:
                    break;
                case Format.Y216:
                    break;
                case Format.NV11:
                    break;
                case Format.AI44:
                    break;
                case Format.IA44:
                    break;
                case Format.P8:
                    break;
                case Format.A8P8:
                    break;
                case Format.P208:
                    break;
                case Format.V208:
                    break;
                case Format.V408:
                    break;
                default:
                    return Guid.Empty;
            }
            
            return Guid.Empty;
        }
    }


}
