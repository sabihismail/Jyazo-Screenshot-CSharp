using System;
using Capture.Interface;
using SharpDX.RawInput;

namespace Capture.Hook.Common
{
    [Serializable]
    public abstract class Element: IOverlayElement, IDisposable
    {
        public virtual bool Hidden { get; set; }
        public virtual bool HandlesMouseInput { get; set; } = false;
        public virtual bool HandlesKeyboardInput { get; set; } = false;

        ~Element()
        {
            Dispose(false);
        }

        public virtual void Frame()
        {
        }

        public virtual void MouseInput(MouseInputEventArgs e, CaptureInterface captureInterface)
        {
        }

        public virtual void KeyboardInput(KeyboardInputEventArgs e, CaptureInterface captureInterface)
        {
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">true if disposing both unmanaged and managed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        protected void SafeDispose(IDisposable disposableObj)
        {
            disposableObj?.Dispose();
        }
    }
}
