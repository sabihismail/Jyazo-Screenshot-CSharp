#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using ScreenShot.Properties;

namespace ScreenShot.src.tools
{
    public static class Logging
    {
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jyazo",
            "logs.txt");

        // ReSharper disable once UnusedMember.Global
        public static void Log(string text, Exception e)
        {
            LogToFile(text, e);
            Debug.WriteLine($"[LOG ERROR] {text} - {e.GetType().Name}: {e.Message}");
        }

        public static void Log(string text)
        {
            LogToFile(text);
            Debug.WriteLine($"[LOG] {text}");
        }

        public static void Log(Exception e)
        {
            LogToFile(e.GetType().Name, e);
            Debug.WriteLine($"[LOG ERROR] {e.GetType().Name}: {e.Message}");
        }

        private static void LogToFile(string message, Exception? exception = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

                if (exception != null)
                    logMessage += Environment.NewLine + exception;

                File.AppendAllText(LogFile, logMessage + Environment.NewLine);
            }
            catch
            {
                // Silently fail if logging itself fails to avoid cascading errors
            }
        }
    }
}
