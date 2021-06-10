using System;
using System.Drawing;

namespace Capture.Hook.Common
{
    [Serializable]
    public class TextElement: Element
    {
        public virtual string Text { get; set; }
        public virtual Font Font { get; } = SystemFonts.DefaultFont;
        public virtual Color Color { get; set; } = Color.Black;
        public virtual Point Location { get; set; }
        public virtual bool AntiAliased { get; set; } = false;

        public TextElement() { }

        public TextElement(Font font)
        {
            Font = font;
        }
    }
}
