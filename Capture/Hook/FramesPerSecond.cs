using System;

namespace Capture.Hook
{
    /// <summary>
    /// Used to determine the FPS
    /// </summary>
    public class FramesPerSecond
    {
        private int frames;
        private int lastTickCount;
        private float lastFrameRate;

        /// <summary>
        /// Must be called each frame
        /// </summary>
        public void Frame()
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
