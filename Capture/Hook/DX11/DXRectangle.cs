using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Bitmap = System.Drawing.Bitmap;
using Color = System.Drawing.Color;
using Device = SharpDX.Direct3D11.Device;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Capture.Hook.DX11
{
    public class DXRectangle : Component
    {
        private Texture2D tex;
        private ShaderResourceView texSrv;
        private bool initialised;
        
        private Device Device { get; }

        public int Width { get; private set; }
        
        public int Height { get; private set; }

        public DXRectangle(Device device) : base("DXRectangle")
        {
            Device = device;
        }
        
        public bool Initialize(float width, float height, Color colour)
        {
            RemoveAndDispose(ref tex);
            RemoveAndDispose(ref texSrv);

            Width = (int) width;
            Height = (int) height;
            
            var texDesc = new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using var bitmapGraphics = Graphics.FromImage(bitmap);
            bitmapGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            bitmapGraphics.Clear(Color.FromArgb(0, Color.Black));
            bitmapGraphics.FillRectangle(new SolidBrush(colour), 0, 0, Width, Height);
            
            var bmData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            
            DataBox data;
            data.DataPointer = bmData.Scan0;
            data.RowPitch = Width * 4;
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