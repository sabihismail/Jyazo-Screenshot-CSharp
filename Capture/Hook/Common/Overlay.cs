using System;
using System.Collections.Generic;

namespace Capture.Hook.Common
{
    [Serializable]
    public class Overlay: IOverlay
    {
        private List<IOverlayElement> elements = new List<IOverlayElement>();
        public virtual List<IOverlayElement> Elements
        {
            get => elements;
            set => elements = value;
        }

        public virtual bool Hidden
        {
            get;
            set;
        }

        public virtual void Frame()
        {
            foreach (var element in Elements)
            {
                element.Frame();
            }
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
