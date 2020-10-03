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
    }
}
