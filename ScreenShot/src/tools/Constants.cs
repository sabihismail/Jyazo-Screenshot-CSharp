using System;
using System.IO;

namespace ScreenShot.src.tools
{
    public static class Constants
    {
        private static readonly string SAVE_DIRECTORY = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\" + CREATOR + "\\" + PROGRAM_NAME + "\\";

        private const string PROGRAM_NAME = "Jyazo";

        private const string CREATOR = "ArkaPrime";

        public const string GITHUB = "https://github.com/sabihismail/Jyazo-Screenshot/tree/master/server";

        public const string DEFAULT_IMAGE_SHORTCUT = "Ctrl Shift C";

        public const string DEFAULT_GIF_SHORTCUT = "Ctrl Shift G";

        public static readonly string DEFAULT_ALL_IMAGES_FOLDER = SAVE_DIRECTORY + "All Images\\";

        public static readonly string SETTINGS_FILE =
#if DEBUG
            Directory.GetCurrentDirectory() + "\\settings.json";
#else
            SAVE_DIRECTORY + "settings.json";
#endif

        public static readonly string CONFIG_FILE =
#if DEBUG
            Directory.GetCurrentDirectory() + "\\config.json";
#else
            SAVE_DIRECTORY + "config.json";
#endif

        public const string API_ENDPOINT_IS_AUTHORIZED = "api/authenticate";

        public const string API_ENDPOINT_UPLOAD_SCREENSHOT = "uploadScreenShot";

        public const string USER_AGENT = $"{CREATOR}/{PROGRAM_NAME}/1.0";

        public static string OVERRIDE_SERVER => GetDevServer();

        /// <summary>
        /// Get dev server from environment variable or use default port 3000
        /// Set DEV_SERVER env var to override, e.g., "http://localhost:3001"
        /// </summary>
        private static string GetDevServer()
        {
            var envServer = Environment.GetEnvironmentVariable("DEV_SERVER");
            if (!string.IsNullOrWhiteSpace(envServer))
            {
                return envServer;
            }

            // Default to port 3000 (can be overridden via DEV_SERVER env var)
            return "http://localhost:3000";
        }
    }
}
