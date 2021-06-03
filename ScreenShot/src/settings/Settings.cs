using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScreenShot.src.tools;
using ScreenShot.views;

namespace ScreenShot.src
{
    public class Settings
    {
        public bool EnableGIF = true;
        
        public bool SaveAllImages = false;
        
        public string SaveDirectory = Constants.DEFAULT_ALL_IMAGES_FOLDER;
        
        public bool EnableImageShortcut = false;
       
        public bool EnableGIFShortcut = false;
        
        public string CaptureImageShortcut = Constants.DEFAULT_IMAGE_SHORTCUT;
        
        public string CaptureGIFShortcut = Constants.DEFAULT_GIF_SHORTCUT;
        
        public List<Key> CaptureImageShortcutKeys = StringToKeys(Constants.DEFAULT_IMAGE_SHORTCUT);
        
        public List<Key> CaptureGIFShortcutKeys = StringToKeys(Constants.DEFAULT_GIF_SHORTCUT);
        
        public bool EnablePrintScreen = false;

        public bool EnableSound = false;

        public Settings()
        {
            if (File.Exists(Constants.SETTINGS_FILE))
            {
                UpdateSettings();
            }
            else
            {
                SaveSettings(EnableGIF, SaveAllImages, SaveDirectory, EnableImageShortcut, CaptureImageShortcutKeys, EnableGIFShortcut, CaptureGIFShortcutKeys, EnablePrintScreen, EnableSound);
            }

            if (File.Exists(Constants.CONFIG_FILE)) return;

            Logging.Log($"This must be your first run. Please input your server's image upload host location. An example php host file is located at {Constants.GITHUB}.");

            var settingsWindow = new SettingsWindow(this, new Config());
            settingsWindow.Show();
        }

        public void SaveSettings(bool enableGIF, bool saveAllImages, String saveDirectory, bool enableImageShortcut, List<Key> imageShortcutKeys, bool enableGIFShortcut, List<Key> gifShortcutKeys, bool enablePrintScreen, bool enableSound)
        {
            EnableGIF = enableGIF;
            SaveAllImages = saveAllImages;
            SaveDirectory = string.IsNullOrWhiteSpace(saveDirectory) ? Constants.DEFAULT_ALL_IMAGES_FOLDER : saveDirectory;
            EnableImageShortcut = enableImageShortcut;
            EnableGIFShortcut = enableGIFShortcut;
            CaptureImageShortcutKeys = imageShortcutKeys;
            CaptureGIFShortcutKeys = gifShortcutKeys;
            EnablePrintScreen = enablePrintScreen;
            EnableSound = enableSound;

            try
            {
                var settingsFile = Constants.SETTINGS_FILE;
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile) ?? throw new InvalidOperationException("settingsFile null"));
                
                var KeysString = KeysToString(imageShortcutKeys);
                var KeysString2 = KeysToString(gifShortcutKeys);

                if (string.IsNullOrWhiteSpace(saveDirectory))
                {
                    saveDirectory = SaveDirectory;
                }

                var settingsContainer = new SettingsContainer
                {
                    EnableGIF = enableGIF,
                    SaveAllImages = saveAllImages,
                    EnablePrintScreen = enablePrintScreen,
                    EnableSound = enableSound,
                    SaveDirectory = saveDirectory,
                    EnableImageShortcut = enableImageShortcut,
                    EnableGIFShortcut = enableGIFShortcut,
                    ImageKeys = KeysString,
                    GifKeys = KeysString2
                };

                var jsonStr = JsonConvert.SerializeObject(settingsContainer);

                File.WriteAllText(settingsFile, JToken.Parse(jsonStr).ToString());
            }
            catch (IOException e)
            {
                Logging.Log(e);
            }
        }
        
        private void UpdateSettings()
        {
            var settingsFile = Constants.SETTINGS_FILE;

            try
            {
                var jsonStr = File.ReadAllText(settingsFile);

                var configContainer = JsonConvert.DeserializeObject<SettingsContainer>(jsonStr);

                EnableGIF = configContainer.EnableGIF;
                SaveAllImages = configContainer.SaveAllImages;
                EnablePrintScreen = configContainer.EnablePrintScreen;
                EnableSound = configContainer.EnableSound;
                SaveDirectory = !string.IsNullOrWhiteSpace(configContainer.SaveDirectory) ? configContainer.SaveDirectory : "";
                EnableImageShortcut = configContainer.EnableImageShortcut;
                CaptureImageShortcut = !string.IsNullOrWhiteSpace(configContainer.ImageKeys) ? configContainer.ImageKeys : "";
                CaptureImageShortcutKeys = !string.IsNullOrWhiteSpace(configContainer.ImageKeys) ? StringToKeys(configContainer.ImageKeys) : new List<Key>();
                EnableGIFShortcut = configContainer.EnableGIFShortcut;
                CaptureGIFShortcut = !string.IsNullOrWhiteSpace(configContainer.GifKeys) ? configContainer.GifKeys : "";
                CaptureGIFShortcutKeys = !string.IsNullOrWhiteSpace(configContainer.GifKeys) ? StringToKeys(configContainer.GifKeys) : new List<Key>();
            }
            catch (Exception)
            {
                if (File.Exists(settingsFile))
                {
                    File.Delete(settingsFile);
                }

                Logging.Log("Settings file is corrupted. File deleted and will be set to default values.");

                SaveSettings(EnableGIF, SaveAllImages, SaveDirectory, EnableImageShortcut, CaptureImageShortcutKeys, EnableGIFShortcut, CaptureGIFShortcutKeys, EnablePrintScreen, EnableSound);
            }
        }

        private static List<Key> StringToKeys(string text)
        {
            var keyConverter = new KeyConverter();

            var split = text.Split(' ');

            return split.Select(s => (Key) keyConverter.ConvertFromString(s))
                .ToList();
        }
        
        public string KeysToString(List<Key> Keys)
        {
            var keyConverter = new KeyConverter();

            if (Keys.Count == 0)
                return "";

            var allKeys = "";
            foreach (var Key in Keys)
            {
                allKeys += keyConverter.ConvertToString(Key) + " ";
            }

            allKeys = allKeys.Substring(0, allKeys.Length - 1);

            return allKeys;
        }
        
        private static List<string> GetListFromString(string text)
        {
            var split = text.Split(' ');

            return split.ToList();
        }
        
        public string GetStringFromKeys(List<string> keys)
        {
            if (keys.Count == 0)
            {
                return "";
            }

            var stringBuilder = new StringBuilder();

            foreach (var key in keys)
            {
                stringBuilder.Append(key).Append(" + ");
            }

            var str = stringBuilder.ToString();

            return str.Substring(0, str.Length - 3);
        }
    }

    public class SettingsContainer
    {
        public bool EnableGIF { get; set; }

        public bool SaveAllImages { get; set; }

        public bool EnablePrintScreen { get; set; }

        public bool EnableSound { get; set; }

        public string SaveDirectory { get; set; }

        public bool EnableImageShortcut { get; set; }

        public bool EnableGIFShortcut { get; set; }

        public string ImageKeys { get; set; }

        public string GifKeys { get; set; }
    }
}
