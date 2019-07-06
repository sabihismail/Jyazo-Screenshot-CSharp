using System;
using System.Windows.Forms;

namespace ScreenShot.src.tools
{
    public static class Logging
    {
        public static void Log(string text, Exception e)
        {
            MessageBox.Show(text + "\n\n" + e, @"Error", MessageBoxButtons.OK);
        }

        public static void Log(string text)
        {
            MessageBox.Show(text, @"Text", MessageBoxButtons.OK);
        }

        public static void Log(Exception e)
        {
            MessageBox.Show(e.ToString(), @"Error", MessageBoxButtons.OK);
        }
    }
}
