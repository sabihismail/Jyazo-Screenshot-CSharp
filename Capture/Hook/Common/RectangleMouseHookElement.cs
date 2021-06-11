using System;
using System.Drawing;
using System.Windows.Forms;
using Capture.Interface;
using SharpDX.RawInput;

namespace Capture.Hook.Common
{
    [Serializable]
    public class RectangleMouseHookElement : RectangleElement
    {
        public override bool Hidden { get; set; } = true;

        public override bool HandlesMouseInput { get; set; } = true;
        
        public override bool HandlesKeyboardInput { get; set; } = true;
        
        private Point? start;
        
        private Point? end;

        public override void MouseInput(MouseInputEventArgs e, CaptureInterface captureInterface)
        {
            captureInterface.Message(MessageType.Debug, "mouse input");
            if (Hidden && end.HasValue) return;
            
            if ((e.ButtonFlags & MouseButtonFlags.LeftButtonDown) == 0)
            {
                var point = new Point(e.X, e.Y);
                
                if (!start.HasValue)
                {
                    start = point;
                }
                else
                {
                    end = point;
                }

                if (!end.HasValue) return;
                
                Location = new Point(Math.Min(start.Value.X, end.Value.X), Math.Min(start.Value.Y, end.Value.Y));
                Width = Math.Abs(start.Value.X - end.Value.X);
                Height = Math.Abs(start.Value.Y - end.Value.Y);

                if (!Hidden) Hidden = false;
            }

            if ((e.ButtonFlags & MouseButtonFlags.LeftButtonUp) != 0)
            {
                Hidden = true;
                
                // captureInterface.GetScreenshot(new Rectangle(Location, new Size((int) Width, (int) Height)), TimeSpan.FromSeconds(15), null, ImageFormat.Png);
            }
        }

        public override void KeyboardInput(KeyboardInputEventArgs e, CaptureInterface captureInterface)
        {
            if (e.Key != Keys.Escape) return;
            
            Hidden = true;
            captureInterface.Disconnect();
        }
    }
}
