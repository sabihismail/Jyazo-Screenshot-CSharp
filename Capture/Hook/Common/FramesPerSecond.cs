using System;
using System.Drawing;

namespace Capture.Hook.Common
{
    [Serializable]
    public class FramesPerSecond: TextElement
    {
        private string fpsFormat = "{0:N0} fps";
        public override string Text
        {
            get => string.Format(fpsFormat, GetFps());
            set => fpsFormat = value;
        }

        private int frames;
        private int lastTickCount;
        private float lastFrameRate;

        public FramesPerSecond(Font font)
            : base(font)
        {
        }

        /// <summary>
        /// Must be called each frame
        /// </summary>
        public override void Frame()
        {
            frames++;
            if (Math.Abs(Environment.TickCount - lastTickCount) <= 1000) return;
            
            lastFrameRate = (float)frames * 1000 / Math.Abs(Environment.TickCount - lastTickCount);
            lastTickCount = Environment.TickCount;
            frames = 0;
        }

        /// <summary>
        /// Return the current frames per second
        /// </summary>
        /// <returns></returns>
        public float GetFps()
        {
            return lastFrameRate;
        }
    }
}
