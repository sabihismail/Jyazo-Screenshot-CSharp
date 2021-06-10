using System;

namespace Capture.Hook
{
    public class TextDisplay
    {
        private readonly long startTickCount;

        public TextDisplay()
        {
            startTickCount = DateTime.Now.Ticks;
            Display = true;
        }

        /// <summary>
        /// Must be called each frame
        /// </summary>
        public void Frame()
        {
            if (Display && Math.Abs(DateTime.Now.Ticks - startTickCount) > Duration.Ticks)
            {
                Display = false;
            }
        }

        public bool Display { get; private set; }
        public string Text { get; set; }
        public TimeSpan Duration { get; set; }
        public float Remaining
        {
            get
            {
                if (Display)
                {
                    return Math.Abs(DateTime.Now.Ticks - startTickCount) / (float)Duration.Ticks;
                }

                return 0;
            }
        }
    }
}
