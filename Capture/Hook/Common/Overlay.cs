using System;
using System.Collections.Generic;
using System.Linq;
using Capture.Interface;
using SharpDX.RawInput;

namespace Capture.Hook.Common
{
    [Serializable]
    public class Overlay: IOverlay
    {
        private List<IOverlayElement> elements = new();
        
        public virtual List<IOverlayElement> Elements
        {
            get => elements;
            set
            {
                elements = value;

                ElementsWithMouseHandling = value.Where(x => x.HandlesMouseInput)
                    .ToList();

                HandlesMouseInput = ElementsWithMouseHandling.Count > 0;

                ElementsWithKeyboardHandling = value.Where(x => x.HandlesKeyboardInput)
                    .ToList();

                HandlesKeyboardInput = ElementsWithKeyboardHandling.Count > 0;
            }
        }
        
        public List<IOverlayElement> ElementsWithMouseHandling { get; set; }
        
        public List<IOverlayElement> ElementsWithKeyboardHandling { get; set; }

        public virtual bool Hidden
        {
            get;
            set;
        }
        
        public virtual bool HandlesMouseInput
        {
            get;
            set;
        }
        
        public virtual bool HandlesKeyboardInput
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

        public void MouseInput(MouseInputEventArgs e, CaptureInterface captureInterface)
        {
            foreach (var element in ElementsWithMouseHandling)
            {
                element.MouseInput(e, captureInterface);
            }
        }

        public void KeyboardInput(KeyboardInputEventArgs e, CaptureInterface captureInterface)
        {
            foreach (var element in ElementsWithKeyboardHandling)
            {
                element.KeyboardInput(e, captureInterface);
            }
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
