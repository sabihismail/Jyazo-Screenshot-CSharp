using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using Capture.Hook.Common;
using Capture.Interface;
using EasyHook;
using SharpDX;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Capture.Hook
{
    internal abstract class BaseDXHook: Component, IDXHook
    {
        private readonly ClientCaptureInterfaceEventProxy interfaceEventProxy = new ClientCaptureInterfaceEventProxy();

        protected BaseDXHook(CaptureInterface ssInterface)
        {
            Interface = ssInterface;
            Timer = new Stopwatch();
            Timer.Start();
            Fps = new FramesPerSecond();

            Interface.ScreenshotRequested += interfaceEventProxy.ScreenshotRequestedProxyHandler;
            Interface.DisplayText += interfaceEventProxy.DisplayTextProxyHandler;
            Interface.DrawOverlay += interfaceEventProxy.DrawOverlayProxyHandler;
            interfaceEventProxy.ScreenshotRequested += InterfaceEventProxy_ScreenshotRequested;
            interfaceEventProxy.DisplayText += InterfaceEventProxy_DisplayText;
            interfaceEventProxy.DrawOverlay += InterfaceEventProxy_DrawOverlay;
            
            Interface.SafeInvokeConnected();
        }

        ~BaseDXHook()
        {
            Dispose(false);
        }

        private void InterfaceEventProxy_DisplayText(DisplayTextEventArgs args)
        {
            TextDisplay = new TextDisplay
            {
                Text = args.Text,
                Duration = args.Duration
            };
        }

        protected virtual void InterfaceEventProxy_ScreenshotRequested(ScreenshotRequest requestIn)
        {
            Request = requestIn;
        }

        private void InterfaceEventProxy_DrawOverlay(DrawOverlayEventArgs args)
        {
            Overlays = new List<IOverlay>();
            if (args.Overlay != null)
                Overlays.Add(args.Overlay);
            IsOverlayUpdatePending = true;
        }

        private Stopwatch Timer { get; set; }

        /// <summary>
        /// Frames Per second counter, FPS.Frame() must be called each frame
        /// </summary>
        protected FramesPerSecond Fps { get; set; }

        protected TextDisplay TextDisplay { get; set; }

        protected List<IOverlay> Overlays { get; set; }
 
        protected bool IsOverlayUpdatePending { get; set; }

        private int processId;
        protected int ProcessId
        {
            get
            {
                if (processId == 0)
                {
                    processId = RemoteHooking.GetCurrentProcessId();
                }
                return processId;
            }
        }

        protected virtual string HookName
        {
            get
            {
                return "BaseDXHook";
            }
        }

        protected void Frame()
        {
            Fps.Frame();
            if (TextDisplay != null && TextDisplay.Display) 
                TextDisplay.Frame();
        }

        protected void DebugMessage(string message)
        {
#if DEBUG
            try
            {
                Interface.Message(MessageType.Debug, HookName + ": " + message);
            }
            catch (RemotingException)
            {
                // Ignore remoting exceptions
            }
            catch (Exception)
            {
                // Ignore all other exceptions
            }
#endif
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int startIndex, int numberOfMethods)
        {
            var vtblAddresses = new List<IntPtr>();

            var vTable = Marshal.ReadIntPtr(pointer);
            for (var i = startIndex; i < startIndex + numberOfMethods; i++)
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size)); // using IntPtr.Size allows us to support both 32 and 64-bit processes

            return vtblAddresses.ToArray();
        }

        protected static void CopyStream(Stream input, Stream output)
        {
            var bufferSize = 32768;
            var buffer = new byte[bufferSize];
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }
                output.Write(buffer, 0, read);
            }
        }

        /// <summary>
        /// Reads data from a stream until the end is reached. The
        /// data is returned as a byte array. An IOException is
        /// thrown if any of the underlying IO calls fail.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        protected static byte[] ReadFullStream(Stream stream)
        {
            if (stream is MemoryStream)
            {
                return ((MemoryStream)stream).ToArray();
            }

            var buffer = new byte[32768];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                        ms.Write(buffer, 0, read);
                    if (read < buffer.Length)
                    {
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Process the capture based on the requested format.
        /// </summary>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <param name="pitch">data pitch (bytes per row)</param>
        /// <param name="format">target format</param>
        /// <param name="pBits">IntPtr to the image data</param>
        /// <param name="requestIn">The original requets</param>
        protected void ProcessCapture(int width, int height, int pitch, PixelFormat format, IntPtr pBits, ScreenshotRequest requestIn)
        {
            if (requestIn == null)
                return;

            if (format == PixelFormat.Undefined)
            {
                DebugMessage("Unsupported render target format");
                return;
            }

            // Copy the image data from the buffer
            var size = height * pitch;
            var data = new byte[size];
            Marshal.Copy(pBits, data, 0, size);

            // Prepare the response
            Screenshot response;

            if (requestIn.Format == Capture.Interface.ImageFormat.PixelData)
            {
                // Return the raw data
                response = new Screenshot(requestIn.RequestId, data)
                {
                    Format = requestIn.Format,
                    PixelFormat = format,
                    Height = height,
                    Width = width,
                    Stride = pitch
                };
            }
            else 
            {
                // Return an image
                using (var bm = data.ToBitmap(width, height, pitch, format))
                {
                    var imgFormat = ImageFormat.Bmp;
                    switch (requestIn.Format)
                    {
                        case Capture.Interface.ImageFormat.Jpeg:
                            imgFormat = ImageFormat.Jpeg;
                            break;
                        case Capture.Interface.ImageFormat.Png:
                            imgFormat = ImageFormat.Png;
                            break;
                    }

                    response = new Screenshot(requestIn.RequestId, bm.ToByteArray(imgFormat))
                    {
                        Format = requestIn.Format,
                        Height = bm.Height,
                        Width = bm.Width
                    };
                }
            }

            // Send the response
            SendResponse(response);
        }

        protected void SendResponse(Screenshot response)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    Interface.SendScreenshotResponse(response);
                    LastCaptureTime = Timer.Elapsed;
                }
                catch (RemotingException)
                {
                    // Ignore remoting exceptions
                    // .NET Remoting will throw an exception if the host application is unreachable
                }
                catch (Exception e)
                {
                    DebugMessage(e.ToString());
                }
            });
        }

        protected void ProcessCapture(Stream stream, ScreenshotRequest requestIn)
        {
            ProcessCapture(ReadFullStream(stream), requestIn);
        }

        private void ProcessCapture(byte[] bitmapData, ScreenshotRequest requestIn)
        {
            try
            {
                if (requestIn != null)
                {
                    Interface.SendScreenshotResponse(new Screenshot(requestIn.RequestId, bitmapData)
                    {
                        Format = requestIn.Format,
                    });
                }
                LastCaptureTime = Timer.Elapsed;
            }
            catch (RemotingException)
            {
                // Ignore remoting exceptions
                // .NET Remoting will throw an exception if the host application is unreachable
            }
            catch (Exception e)
            {
                DebugMessage(e.ToString());
            }
        }

        // ReSharper disable once UnusedMember.Local
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();

            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
        }

        // ReSharper disable once UnusedMember.Local
        private Bitmap BitmapFromBytes(byte[] bitmapData)
        {
            using (var ms = new MemoryStream(bitmapData))
            {
                return (Bitmap)Image.FromStream(ms);
            }
        }

        protected TimeSpan LastCaptureTime
        {
            get;
            set;
        }

        protected bool CaptureThisFrame
        {
            get
            {
                return ((Timer.Elapsed - LastCaptureTime) > CaptureDelay) || Request != null;
            }
        }
        protected TimeSpan CaptureDelay { get; set; }

        #region IDXHook Members

        public CaptureInterface Interface
        {
            get;
            set;
        }
        
        private CaptureConfig config;
        public CaptureConfig Config
        {
            get { return config; }
            set
            {
                config = value;
                CaptureDelay = new TimeSpan(0, 0, 0, 0, (int)((1.0 / config.TargetFramesPerSecond) * 1000.0));
            }
        }

        private ScreenshotRequest request;
        public ScreenshotRequest Request
        {
            get { return request; }
            set { Interlocked.Exchange(ref request, value);  }
        }

        protected List<Hook> Hooks = new List<Hook>();
        public abstract void Hook();

        public abstract void Cleanup();

        #endregion

        #region IDispose Implementation

        protected override void Dispose(bool disposeManagedResources)
        {
            // Only clean up managed objects if disposing (i.e. not called from destructor)
            if (disposeManagedResources)
            {
                try
                {
                    Cleanup();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    // Uninstall Hooks
                    if (Hooks.Count > 0)
                    {
                        // First disable the hook (by excluding all threads) and wait long enough to ensure that all hooks are not active
                        foreach (var hook in Hooks)
                        {
                            // Lets ensure that no threads will be intercepted again
                            hook.Deactivate();
                        }

                        Thread.Sleep(100);

                        // Now we can dispose of the hooks (which triggers the removal of the hook)
                        foreach (var hook in Hooks)
                        {
                            hook.Dispose();
                        }

                        Hooks.Clear();
                    }

                    try
                    {
                        // Remove the event handlers
                        Interface.ScreenshotRequested -= interfaceEventProxy.ScreenshotRequestedProxyHandler;
                        Interface.DisplayText -= interfaceEventProxy.DisplayTextProxyHandler;
                        Interface.DrawOverlay -= InterfaceEventProxy_DrawOverlay;
                    }
                    catch (RemotingException) { } // Ignore remoting exceptions (host process may have been closed)
                }
                catch
                {
                    // ignored
                }
            }

            base.Dispose(disposeManagedResources);
        }

        #endregion
    }
}
