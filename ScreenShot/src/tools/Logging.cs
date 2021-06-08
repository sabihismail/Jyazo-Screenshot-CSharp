using System;
using System.Windows.Forms;
using ScreenShot.Properties;

namespace ScreenShot.src.tools
{
    public static class Logging
    {
        // ReSharper disable once UnusedMember.Global
        public static void Log(string text, Exception e)
        {
            MessageBox.Show(text + Resources.Logging_Log_DoubleNewLine + e, @"Error", MessageBoxButtons.OK);
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
