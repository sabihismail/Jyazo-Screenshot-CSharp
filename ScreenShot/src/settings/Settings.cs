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
        public bool enableGIF = true;
        public bool saveAllImages = true;
        public string saveDirectory = Constants.DEFAULT_ALL_IMAGES_FOLDER;
        public bool enableImageShortcut = true;
        public bool enableGIFShortcut = true;
        public string captureImageShortcut = Constants.DEFAULT_IMAGE_SHORTCUT;
        public string captureGIFShortcut = Constants.DEFAULT_GIF_SHORTCUT;
        public List<string> keys = getListFromString(Constants.DEFAULT_IMAGE_SHORTCUT);
        public List<string> keys2 = getListFromString(Constants.DEFAULT_GIF_SHORTCUT);
        public List<Key> Keys = stringToKeys(Constants.DEFAULT_IMAGE_SHORTCUT);
        public List<Key> Keys2 = stringToKeys(Constants.DEFAULT_GIF_SHORTCUT);
        public bool enablePrintScreen = true;
        public bool enableSound = true;

        public Settings()
        {
            if (File.Exists(Constants.SETTINGS_FILE))
            {
                updateSettings();
            }
            else
            {
                saveSettings(enableGIF, saveAllImages, saveDirectory, enableImageShortcut, Keys, enableGIFShortcut, Keys2, enablePrintScreen, enableSound);
            }

            if (File.Exists(Constants.CONFIG_FILE)) return;

            Logging.Log($"This must be your first run. Please input your server's image upload host location. An example php host file is located at {Constants.GITHUB}.");

            var settingsWindow = new SettingsWindow(this, new Config());
            settingsWindow.Show();
        }

        public void saveSettings(bool enableGIF, bool saveAllImages, String saveDirectory, bool enableImageShortcut, List<Key> imageShortcutKeys, bool enableGIFShortcut, List<Key> gifShortcutKeys, bool enablePrintScreen, bool enableSound)
        {
            this.enableGIF = enableGIF;
            this.saveAllImages = saveAllImages;
            this.saveDirectory = string.IsNullOrWhiteSpace(saveDirectory) ? Constants.DEFAULT_ALL_IMAGES_FOLDER : saveDirectory;
            this.enableImageShortcut = enableImageShortcut;
            this.enableGIFShortcut = enableGIFShortcut;
            this.Keys = imageShortcutKeys;
            this.Keys2 = gifShortcutKeys;
            this.enablePrintScreen = enablePrintScreen;
            this.enableSound = enableSound;

            try
            {
                var settingsFile = Constants.SETTINGS_FILE;
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile) ?? throw new InvalidOperationException("settingsFile null"));
                
                var KeysString = KeysToString(imageShortcutKeys);
                var KeysString2 = KeysToString(gifShortcutKeys);

                if (string.IsNullOrWhiteSpace(saveDirectory))
                {
                    saveDirectory = this.saveDirectory;
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
                    Keys = KeysString,
                    Keys2 = KeysString2
                };

                var jsonStr = JsonConvert.SerializeObject(settingsContainer);

                File.WriteAllText(settingsFile, JToken.Parse(jsonStr).ToString());
            }
            catch (IOException e)
            {
                Logging.Log(e);
            }
        }
        
        private void updateSettings()
        {
            var settingsFile = Constants.SETTINGS_FILE;

            try
            {
                var jsonStr = File.ReadAllText(settingsFile);

                var configContainer = JsonConvert.DeserializeObject<SettingsContainer>(jsonStr);

                enableGIF = configContainer.EnableGIF;
                saveAllImages = configContainer.SaveAllImages;
                enablePrintScreen = configContainer.EnablePrintScreen;
                enableSound = configContainer.EnableSound;
                saveDirectory = !string.IsNullOrWhiteSpace(configContainer.SaveDirectory) ? configContainer.SaveDirectory : "";
                enableImageShortcut = configContainer.EnableImageShortcut;
                captureImageShortcut = !string.IsNullOrWhiteSpace(configContainer.Keys) ? configContainer.Keys : "";
                keys = !string.IsNullOrWhiteSpace(configContainer.Keys) ? getListFromString(configContainer.Keys) : new List<string>();
                Keys = !string.IsNullOrWhiteSpace(configContainer.Keys) ? stringToKeys(configContainer.Keys) : new List<Key>();
                enableGIFShortcut = configContainer.EnableGIFShortcut;
                captureGIFShortcut = !string.IsNullOrWhiteSpace(configContainer.Keys2) ? configContainer.Keys2 : "";
                keys2 = !string.IsNullOrWhiteSpace(configContainer.Keys2) ? getListFromString(configContainer.Keys2) : new List<string>();
                Keys2 = !string.IsNullOrWhiteSpace(configContainer.Keys2) ? stringToKeys(configContainer.Keys2) : new List<Key>();
            }
            catch (Exception)
            {
                if (File.Exists(settingsFile))
                {
                    File.Delete(settingsFile);
                }

                Logging.Log("Settings file is corrupted. File deleted and will be set to default values.");

                saveSettings(enableGIF, saveAllImages, saveDirectory, enableImageShortcut, Keys, enableGIFShortcut, Keys2, enablePrintScreen, enableSound);
            }
        }

        private static List<Key> stringToKeys(string text)
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
        
        private static List<string> getListFromString(string text)
        {
            var split = text.Split(' ');

            return split.ToList();
        }
        
        public string getStringFromKeys(List<string> keys)
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
        public string Keys { get; set; }
        public string Keys2 { get; set; }
    }
}
