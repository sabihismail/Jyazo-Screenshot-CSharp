using System;
using Capture.Interface;
using SharpDX.RawInput;

namespace Capture.Hook.Common
{
    public interface IOverlayElement : ICloneable
    {
        bool Hidden { get; set; }
        
        bool HandlesMouseInput { get; set; }
        
        bool HandlesKeyboardInput { get; set; }

        void Frame();

        void MouseInput(MouseInputEventArgs e, CaptureInterface captureInterface);

        void KeyboardInput(KeyboardInputEventArgs e, CaptureInterface captureInterface);
    }
}
