using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Capture.Hook.DX9;
using Capture.Interface;
using SharpDX;
using SharpDX.Direct3D9; //using SlimDX.Direct3D9;

namespace Capture.Hook
{
    internal class DXHookD3D9: BaseDXHook
    {
        public DXHookD3D9(CaptureInterface ssInterface)
            : base(ssInterface)
        {
        }

        private Hook<Direct3D9DeviceEndSceneDelegate> direct3DDeviceEndSceneHook;
        private Hook<Direct3D9DeviceResetDelegate> direct3DDeviceResetHook;
        private Hook<Direct3D9DevicePresentDelegate> direct3DDevicePresentHook;
        private Hook<Direct3D9DeviceExPresentExDelegate> direct3DDeviceExPresentExHook;
        private readonly object lockRenderTarget = new object();

        private bool resourcesInitialised;
        private Query query;
        private Font font;
        private bool queryIssued;
        private ScreenshotRequest requestCopy;
        private bool renderTargetCopyLocked;
        private Surface renderTargetCopy;
        private Surface resolvedTarget;

        protected override string HookName => "DXHookD3D9";

        private List<IntPtr> id3dDeviceFunctionAddresses = new List<IntPtr>();
        //List<IntPtr> id3dDeviceExFunctionAddresses = new List<IntPtr>();
        private const int D_3D9_DEVICE_METHOD_COUNT = 119;
        private const int D_3D9_EX_DEVICE_METHOD_COUNT = 15;
        private bool supportsDirect3D9Ex;
        public override void Hook()
        {
            DebugMessage("Hook: Begin");
            // First we need to determine the function address for IDirect3DDevice9
            id3dDeviceFunctionAddresses = new List<IntPtr>();
            //id3dDeviceExFunctionAddresses = new List<IntPtr>();
            DebugMessage("Hook: Before device creation");
            using (var d3d = new Direct3D())
            {
                using (var renderForm = new Form())
                {
                    Device device;
                    using (device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle }))
                    {
                        DebugMessage("Hook: Device created");
                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D_3D9_DEVICE_METHOD_COUNT));
                    }
                }
            }

            try
            {
                using (var d3dEx = new Direct3DEx())
                {
                    DebugMessage("Hook: Direct3DEx...");
                    using (var renderForm = new Form())
                    {
                        using (var deviceEx = new DeviceEx(d3dEx, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle }, new DisplayModeEx { Width = 800, Height = 600 }))
                        {
                            DebugMessage("Hook: DeviceEx created - PresentEx supported");
                            id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(deviceEx.NativePointer, D_3D9_DEVICE_METHOD_COUNT, D_3D9_EX_DEVICE_METHOD_COUNT));
                            supportsDirect3D9Ex = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                supportsDirect3D9Ex = false;
            }

            // We want to hook each method of the IDirect3DDevice9 interface that we are interested in

            // 42 - EndScene (we will retrieve the back buffer here)
            direct3DDeviceEndSceneHook = new Hook<Direct3D9DeviceEndSceneDelegate>(
                id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.EndScene],
                // On Windows 7 64-bit w/ 32-bit app and d3d9 dll version 6.1.7600.16385, the address is equiv to:
                // (IntPtr)(GetModuleHandle("d3d9").ToInt32() + 0x1ce09),
                // A 64-bit app would use 0xff18
                // Note: GetD3D9DeviceFunctionAddress will output these addresses to a log file
                new Direct3D9DeviceEndSceneDelegate(EndSceneHook),
                this);

            unsafe
            {
                // If Direct3D9Ex is available - hook the PresentEx
                if (supportsDirect3D9Ex)
                {
                    direct3DDeviceExPresentExHook = new Hook<Direct3D9DeviceExPresentExDelegate>(
                        id3dDeviceFunctionAddresses[(int)Direct3DDevice9ExFunctionOrdinals.PresentEx],
                        new Direct3D9DeviceExPresentExDelegate(PresentExHook),
                        this);
                }

                // Always hook Present also (device will only call Present or PresentEx not both)
                direct3DDevicePresentHook = new Hook<Direct3D9DevicePresentDelegate>(
                    id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Present],
                    new Direct3D9DevicePresentDelegate(PresentHook),
                    this);
            }

            // 16 - Reset (called on resolution change or windowed/fullscreen change - we will reset some things as well)
            direct3DDeviceResetHook = new Hook<Direct3D9DeviceResetDelegate>(
                id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Reset],
                // On Windows 7 64-bit w/ 32-bit app and d3d9 dll version 6.1.7600.16385, the address is equiv to:
                //(IntPtr)(GetModuleHandle("d3d9").ToInt32() + 0x58dda),
                // A 64-bit app would use 0x3b3a0
                // Note: GetD3D9DeviceFunctionAddress will output these addresses to a log file
                new Direct3D9DeviceResetDelegate(ResetHook),
                this);

            /*
             * Don't forget that all hooks will start deactivated...
             * The following ensures that all threads are intercepted:
             * Note: you must do this for each hook.
             */
            
            direct3DDeviceEndSceneHook.Activate();
            Hooks.Add(direct3DDeviceEndSceneHook);

            direct3DDevicePresentHook.Activate();
            Hooks.Add(direct3DDevicePresentHook);

            if (supportsDirect3D9Ex)
            {
                direct3DDeviceExPresentExHook.Activate();
                Hooks.Add(direct3DDeviceExPresentExHook);
            }

            direct3DDeviceResetHook.Activate();
            Hooks.Add(direct3DDeviceResetHook);

            DebugMessage("Hook: End");
        }

        /// <summary>
        /// Just ensures that the surface we created is cleaned up.
        /// </summary>
        public override void Cleanup()
        {
            lock (lockRenderTarget)
            {
                resourcesInitialised = false;

                RemoveAndDispose(ref renderTargetCopy);
                renderTargetCopyLocked = false;

                RemoveAndDispose(ref resolvedTarget);
                RemoveAndDispose(ref query);
                queryIssued = false;

                RemoveAndDispose(ref font);

                RemoveAndDispose(ref overlayEngine);
            }
        }

        /// <summary>
        /// The IDirect3DDevice9.EndScene function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int Direct3D9DeviceEndSceneDelegate(IntPtr device);

        /// <summary>
        /// The IDirect3DDevice9.Reset function definition
        /// </summary>
        /// <param name="device"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int Direct3D9DeviceResetDelegate(IntPtr device, ref PresentParameters presentParameters);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private unsafe delegate int Direct3D9DevicePresentDelegate(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private unsafe delegate int Direct3D9DeviceExPresentExDelegate(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags);
        

        /// <summary>
        /// Reset the _renderTarget so that we are sure it will have the correct presentation parameters (required to support working across changes to windowed/fullscreen or resolution changes)
        /// </summary>
        /// <param name="devicePtr"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        private int ResetHook(IntPtr devicePtr, ref PresentParameters presentParameters)
        {
            // Ensure certain overlay resources have performed necessary pre-reset tasks
            if (overlayEngine != null)
                overlayEngine.BeforeDeviceReset();

            Cleanup();

            return direct3DDeviceResetHook.Original(devicePtr, ref presentParameters);
        }

        private bool isUsingPresent;

        // Used in the overlay
        private unsafe int PresentExHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags)
        {
            isUsingPresent = true;
            var device = (DeviceEx)devicePtr;

            DoCaptureRenderTarget(device, "PresentEx");

            return direct3DDeviceExPresentExHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion, dwFlags);
        }

        private unsafe int PresentHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            isUsingPresent = true;

            var device = (Device)devicePtr;

            DoCaptureRenderTarget(device, "PresentHook");

            return direct3DDevicePresentHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
        }

        /// <summary>
        /// Hook for IDirect3DDevice9.EndScene
        /// </summary>
        /// <param name="devicePtr">Pointer to the IDirect3DDevice9 instance. Note: object member functions always pass "this" as the first parameter.</param>
        /// <returns>The HRESULT of the original EndScene</returns>
        /// <remarks>Remember that this is called many times a second by the Direct3D application - be mindful of memory and performance!</remarks>
        private int EndSceneHook(IntPtr devicePtr)
        {
            var device = (Device)devicePtr;

            if (!isUsingPresent)
                DoCaptureRenderTarget(device, "EndSceneHook");

            return direct3DDeviceEndSceneHook.Original(devicePtr);
        }

        private DXOverlayEngine overlayEngine;

        /// <summary>
        /// Implementation of capturing from the render target of the Direct3D9 Device (or DeviceEx)
        /// </summary>
        /// <param name="device"></param>
        /// <param name="hook"></param>
        private void DoCaptureRenderTarget(Device device, string hook)
        {
            Frame();
            
            try
            {
                #region Screenshot Request

                // If we have issued the command to copy data to our render target, check if it is complete
                if (queryIssued && requestCopy != null && query.GetData(out bool _, false))
                {
                    // The GPU has finished copying data to _renderTargetCopy, we can now lock
                    // the data and access it on another thread.

                    queryIssued = false;
                    
                    // Lock the render target
                    var lockedRect = LockRenderTarget(renderTargetCopy, out var rect);
                    renderTargetCopyLocked = true;

                    // Copy the data from the render target
                    Task.Factory.StartNew(() =>
                    {
                        lock (lockRenderTarget)
                        {
                            ProcessCapture(rect.Width, rect.Height, lockedRect.Pitch, renderTargetCopy.Description.Format.ToPixelFormat(), lockedRect.DataPointer, requestCopy);
                        }
                    });
                }

                // Single frame capture request
                if (Request != null)
                {
                    var start = DateTime.Now;
                    try
                    {
                        using (var renderTarget = device.GetRenderTarget(0))
                        {
                            int width, height;

                            // If resizing of the captured image, determine correct dimensions
                            if (Request.Resize != null && (renderTarget.Description.Width > Request.Resize.Value.Width || renderTarget.Description.Height > Request.Resize.Value.Height))
                            {
                                if (renderTarget.Description.Width > Request.Resize.Value.Width)
                                {
                                    width = Request.Resize.Value.Width;
                                    height = (int)Math.Round((renderTarget.Description.Height * (Request.Resize.Value.Width / (double)renderTarget.Description.Width)));
                                }
                                else
                                {
                                    height = Request.Resize.Value.Height;
                                    width = (int)Math.Round((renderTarget.Description.Width * (Request.Resize.Value.Height / (double)renderTarget.Description.Height)));
                                }
                            }
                            else
                            {
                                width = renderTarget.Description.Width;
                                height = renderTarget.Description.Height;
                            }

                            // If existing _renderTargetCopy, ensure that it is the correct size and format
                            if (renderTargetCopy != null && (renderTargetCopy.Description.Width != width || renderTargetCopy.Description.Height != height || renderTargetCopy.Description.Format != renderTarget.Description.Format))
                            {
                                // Cleanup resources
                                Cleanup();
                            }

                            // Ensure that we have something to put the render target data into
                            if (!resourcesInitialised || renderTargetCopy == null)
                            {
                                CreateResources(device, width, height, renderTarget.Description.Format);
                            }

                            // Resize from render target Surface to resolvedSurface (also deals with resolving multi-sampling)
                            device.StretchRectangle(renderTarget, resolvedTarget, TextureFilter.None);
                        }

                        // If the render target is locked from a previous request unlock it
                        if (renderTargetCopyLocked)
                        {
                            // Wait for the the ProcessCapture thread to finish with it
                            lock (lockRenderTarget)
                            {
                                if (renderTargetCopyLocked)
                                {
                                    renderTargetCopy.UnlockRectangle();
                                    renderTargetCopyLocked = false;
                                }
                            }
                        }
                            
                        // Copy data from resolved target to our render target copy
                        device.GetRenderTargetData(resolvedTarget, renderTargetCopy);

                        requestCopy = Request.Clone();
                        query.Issue(Issue.End);
                        queryIssued = true;
                        
                    }
                    finally
                    {
                        // We have completed the request - mark it as null so we do not continue to try to capture the same request
                        // Note: If you are after high frame rates, consider implementing buffers here to capture more frequently
                        //         and send back to the host application as needed. The IPC overhead significantly slows down 
                        //         the whole process if sending frame by frame.
                        Request = null;
                    }
                    var end = DateTime.Now;
                    DebugMessage(hook + ": Capture time: " + (end - start));
                }

                #endregion

                var displayOverlays = Overlays;
                if (Config.ShowOverlay && displayOverlays != null)
                {
                    #region Draw Overlay

                    // Check if overlay needs to be initialised
                    if (overlayEngine == null || overlayEngine.Device.NativePointer != device.NativePointer
                        || IsOverlayUpdatePending)
                    {
                        // Cleanup if necessary
                        if (overlayEngine != null)
                            RemoveAndDispose(ref overlayEngine);

                        overlayEngine = ToDispose(new DXOverlayEngine());
                        overlayEngine.Overlays.AddRange(displayOverlays);
                        overlayEngine.Initialise(device);
                        IsOverlayUpdatePending = false;
                    }
                    // Draw Overlay(s)
                    if (overlayEngine != null)
                    {
                        foreach (var overlay in overlayEngine.Overlays)
                            overlay.Frame();
                        overlayEngine.Draw();
                    }

                    #endregion
                }
            }
            catch (Exception e)
            {
                DebugMessage(e.ToString());
            }
        }

        private DataRectangle LockRenderTarget(Surface renderTargetCopyIn, out Rectangle rect)
        {
            if (requestCopy.RegionToCapture.Height > 0 && requestCopy.RegionToCapture.Width > 0)
            {
                rect = new Rectangle(requestCopy.RegionToCapture.Left, requestCopy.RegionToCapture.Top, requestCopy.RegionToCapture.Width, requestCopy.RegionToCapture.Height);
            }
            else
            {
                rect = new Rectangle(0, 0, renderTargetCopyIn.Description.Width, renderTargetCopyIn.Description.Height);
            }
            return renderTargetCopyIn.LockRectangle(rect, LockFlags.ReadOnly);
        }

        private void CreateResources(Device device, int width, int height, Format format)
        {
            if (resourcesInitialised) return;
            resourcesInitialised = true;
            
            // Create offscreen surface to use as copy of render target data
            renderTargetCopy = ToDispose(Surface.CreateOffscreenPlain(device, width, height, format, Pool.SystemMemory));
            
            // Create our resolved surface (resizing if necessary and to resolve any multi-sampling)
            resolvedTarget = ToDispose(Surface.CreateRenderTarget(device, width, height, format, MultisampleType.None, 0, false));

            query = ToDispose(new Query(device, QueryType.Event));
        }
    }
}
