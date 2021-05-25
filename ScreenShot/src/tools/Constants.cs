using System;

namespace ScreenShot.src.tools
{
    public class Constants
    {
        public const string PROGRAM_NAME = "Jyazo";

        public const string CREATOR = "ArkaPrime";

        public const string GITHUB = "https://github.com/sabihismail/Jyazo-Screenshot-CSharp";
        
        private static readonly string SAVE_DIRECTORY = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\" + CREATOR + "\\" + PROGRAM_NAME + "\\";
        
        public static readonly string DEFAULT_ALL_IMAGES_FOLDER = SAVE_DIRECTORY + "All Images\\";
        
        public static readonly string DEFAULT_IMAGE_SHORTCUT = "Ctrl Shift C";
        
        public static readonly string DEFAULT_GIF_SHORTCUT = "Ctrl Shift G";
        
        public static readonly string SETTINGS_FILE = SAVE_DIRECTORY + "settings.json";
        
        public static readonly string CONFIG_FILE = SAVE_DIRECTORY + "config.json";

        public const string API_ENDPOINT_IS_AUTHORIZED = "isAuthorized";

        public const string API_ENDPOINT_UPLOAD_SCREENSHOT = "uploadScreenShot";

        public const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Safari/537.36 Edg/90.0.818.66";
    }
}
