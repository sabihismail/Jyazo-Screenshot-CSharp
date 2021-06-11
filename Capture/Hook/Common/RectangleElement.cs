using System;
using System.Drawing;

namespace Capture.Hook.Common
{
    [Serializable]
    public class RectangleElement : Element
    {
        public virtual Color Colour { get; set; } = Color.FromArgb(Color.Gray.ToArgb() ^ (0x33 << 24));

        public virtual Point Location { get; set; }
        
        public virtual float Width { get; set; }
        
        public virtual float Height { get; set; }
    }
}
