using System;
using System.Runtime.Remoting;
using System.Security.Permissions;

namespace Capture.Interface
{
    public sealed class Screenshot : MarshalByRefObject, IDisposable
    {
        public Guid RequestId { get; }

        public ImageFormat Format { get; set; }

        public System.Drawing.Imaging.PixelFormat PixelFormat { get; set; }
        public int Stride { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }

        public byte[] Data { get; }

        private bool disposed;

        public Screenshot(Guid requestId, byte[] data)
        {
            RequestId = requestId;
            Data = data;
        }

        ~Screenshot()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposeManagedResources)
        {
            if (disposed) return;
            
            if (disposeManagedResources)
            {
                Disconnect();
            }
            
            disposed = true;
        }

        /// <summary>
        /// Disconnects the remoting channel(s) of this object and all nested objects.
        /// </summary>
        private void Disconnect()
        {
            RemotingServices.Disconnect(this);
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            // Returning null designates an infinite non-expiring lease.
            // We must therefore ensure that RemotingServices.Disconnect() is called when
            // it's no longer needed otherwise there will be a memory leak.
            return null;
        }
    }
}
