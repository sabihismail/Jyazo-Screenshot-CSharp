using System.Text;

namespace ScreenShot.src.tools
{
    public static class Extensions
    {
        public static byte[] GetBytes(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }
    }
}
