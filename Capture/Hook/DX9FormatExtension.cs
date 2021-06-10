using System.Drawing.Imaging;
using SharpDX.Direct3D9;

namespace Capture.Hook
{
    public static class DX9FormatExtension
    {
        // ReSharper disable once UnusedMember.Global
        public static int ToPixelDepth(this Format format)
        {
            // Only support the DX9 BackBuffer formats: http://msdn.microsoft.com/en-us/library/windows/desktop/bb172558(v=vs.85).aspx
            switch (format)
            {
                case Format.A2R10G10B10:
                case Format.A8R8G8B8:
                case Format.X8R8G8B8:
                    return 32;
                case Format.R5G6B5:
                case Format.A1R5G5B5:
                case Format.X1R5G5B5:
                    return 16;
                
                case Format.Unknown:
                    break;
                case Format.R8G8B8:
                    break;
                case Format.A4R4G4B4:
                    break;
                case Format.R3G3B2:
                    break;
                case Format.A8:
                    break;
                case Format.A8R3G3B2:
                    break;
                case Format.X4R4G4B4:
                    break;
                case Format.A2B10G10R10:
                    break;
                case Format.A8B8G8R8:
                    break;
                case Format.X8B8G8R8:
                    break;
                case Format.G16R16:
                    break;
                case Format.A16B16G16R16:
                    break;
                case Format.A8P8:
                    break;
                case Format.P8:
                    break;
                case Format.L8:
                    break;
                case Format.A8L8:
                    break;
                case Format.A4L4:
                    break;
                case Format.V8U8:
                    break;
                case Format.L6V5U5:
                    break;
                case Format.X8L8V8U8:
                    break;
                case Format.Q8W8V8U8:
                    break;
                case Format.V16U16:
                    break;
                case Format.A2W10V10U10:
                    break;
                case Format.Uyvy:
                    break;
                case Format.R8G8_B8G8:
                    break;
                case Format.Yuy2:
                    break;
                case Format.G8R8_G8B8:
                    break;
                case Format.Dxt1:
                    break;
                case Format.Dxt2:
                    break;
                case Format.Dxt3:
                    break;
                case Format.Dxt4:
                    break;
                case Format.Dxt5:
                    break;
                case Format.D16Lockable:
                    break;
                case Format.D32:
                    break;
                case Format.D15S1:
                    break;
                case Format.D24S8:
                    break;
                case Format.D24X8:
                    break;
                case Format.D24X4S4:
                    break;
                case Format.D16:
                    break;
                case Format.D32SingleLockable:
                    break;
                case Format.D24SingleS8:
                    break;
                case Format.D32Lockable:
                    break;
                case Format.S8Lockable:
                    break;
                case Format.L16:
                    break;
                case Format.VertexData:
                    break;
                case Format.Index16:
                    break;
                case Format.Index32:
                    break;
                case Format.Q16W16V16U16:
                    break;
                case Format.Multi2Argb8:
                    break;
                case Format.R16F:
                    break;
                case Format.G16R16F:
                    break;
                case Format.A16B16G16R16F:
                    break;
                case Format.R32F:
                    break;
                case Format.G32R32F:
                    break;
                case Format.A32B32G32R32F:
                    break;
                case Format.MtCxV8U8:
                    break;
                case Format.A1:
                    break;
                case Format.MtA2B10G10R10XrBias:
                    break;
                case Format.BinaryBuffer:
                    break;
                default:
                    return -1;
            }
            
            return -1;
        }
        
        public static PixelFormat ToPixelFormat(this Format format)
        {
            // Only support the BackBuffer formats: http://msdn.microsoft.com/en-us/library/windows/desktop/bb172558(v=vs.85).aspx
            // and of these only those that have a direct mapping to supported PixelFormat's
            switch (format)
            {
                case Format.A8R8G8B8:
                case Format.X8R8G8B8:
                    return PixelFormat.Format32bppArgb;
                case Format.R5G6B5:
                    return PixelFormat.Format16bppRgb565;
                case Format.A1R5G5B5:
                case Format.X1R5G5B5:
                    return PixelFormat.Format16bppArgb1555;
                default:
                    return PixelFormat.Undefined;
            }
        }
    }
}
