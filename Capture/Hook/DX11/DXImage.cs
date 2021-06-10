using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Rectangle = System.Drawing.Rectangle;

namespace Capture.Hook.DX11
{
    public class DXImage : Component
    {
        // ReSharper disable once NotAccessedField.Local
        private DeviceContext deviceContext;
        private Texture2D tex;
        private ShaderResourceView texSrv;
        private bool initialised;

        public int Width { get; private set; }

        public int Height { get; private set; }

        private Device Device { get; }

        public DXImage(Device device, DeviceContext deviceContext): base("DXImage")
        {
            Device = device;
            this.deviceContext = deviceContext;
            tex = null;
            texSrv = null;
            Width = 0;
            Height = 0;
        }

        public bool Initialise(Bitmap bitmap)
        {
            RemoveAndDispose(ref tex);
            RemoveAndDispose(ref texSrv);

            //Debug.Assert(bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            Width = bitmap.Width;
            Height = bitmap.Height;

            var bmData = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var texDesc = new Texture2DDescription
                {
                    Width = Width,
                    Height = Height,
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
                data.RowPitch = bmData.Stride;// _texWidth * 4;
                data.SlicePitch = 0;

                tex = ToDispose(new Texture2D(Device, texDesc, new[] { data }));
                if (tex == null)
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

                texSrv = ToDispose(new ShaderResourceView(Device, tex, srvDesc));
                if (texSrv == null)
                    return false;
            }
            finally
            {
                bitmap.UnlockBits(bmData);
            }

            initialised = true;

            return true;
        }

        public ShaderResourceView GetSrv()
        {
            Debug.Assert(initialised);
            return texSrv;
        }
    }
}
