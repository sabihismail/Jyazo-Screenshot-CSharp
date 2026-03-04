using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScreenShot.src.tools;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace ScreenShot.src.settings
{
    public record SettingsData(
        bool EnableFullscreenCapture,
        bool EnableGIF,
        bool SaveAllImages,
        string SaveDirectory,
        bool EnableImageShortcut,
        IReadOnlyList<Key> ImageShortcutKeys,
        bool EnableGIFShortcut,
        IReadOnlyList<Key> GifShortcutKeys,
        bool EnablePrintScreen,
        bool EnableSound);

    public class Settings
    {
        public bool EnableFullscreenCapture = true;

        public bool EnableGIF = true;
        
        public bool SaveAllImages;
        
        public string SaveDirectory = Constants.DEFAULT_ALL_IMAGES_FOLDER;
        
        public bool EnableImageShortcut;
       
        public bool EnableGIFShortcut;
        
        public string CaptureImageShortcut = Constants.DEFAULT_IMAGE_SHORTCUT;

        public string CaptureGIFShortcut = Constants.DEFAULT_GIF_SHORTCUT;

        private List<Key> _captureImageShortcutKeys = StringToKeys(Constants.DEFAULT_IMAGE_SHORTCUT);
        private List<Key> _captureGIFShortcutKeys = StringToKeys(Constants.DEFAULT_GIF_SHORTCUT);

        public IReadOnlyList<Key> CaptureImageShortcutKeys => _captureImageShortcutKeys.AsReadOnly();
        public IReadOnlyList<Key> CaptureGIFShortcutKeys => _captureGIFShortcutKeys.AsReadOnly();

        public bool EnablePrintScreen;

        public bool EnableSound;

        public Settings()
        {
            if (File.Exists(Constants.SETTINGS_FILE))
            {
                UpdateSettings();
            }
            else
            {
                SaveSettings(new SettingsData(
                    EnableFullscreenCapture, EnableGIF, SaveAllImages, SaveDirectory, EnableImageShortcut,
                    CaptureImageShortcutKeys, EnableGIFShortcut, CaptureGIFShortcutKeys, EnablePrintScreen, EnableSound));
            }
        }

        public void SaveSettings(SettingsData data)
        {
            EnableFullscreenCapture = data.EnableFullscreenCapture;
            EnableGIF = data.EnableGIF;
            SaveAllImages = data.SaveAllImages;
            SaveDirectory = string.IsNullOrWhiteSpace(data.SaveDirectory) ? Constants.DEFAULT_ALL_IMAGES_FOLDER : data.SaveDirectory;
            EnableImageShortcut = data.EnableImageShortcut;
            EnableGIFShortcut = data.EnableGIFShortcut;
            _captureImageShortcutKeys = new List<Key>(data.ImageShortcutKeys);
            _captureGIFShortcutKeys = new List<Key>(data.GifShortcutKeys);
            EnablePrintScreen = data.EnablePrintScreen;
            EnableSound = data.EnableSound;

            try
            {
                var settingsFile = Constants.SETTINGS_FILE;
                var directory = Path.GetDirectoryName(settingsFile);

                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException("Settings file path is invalid");

                Directory.CreateDirectory(directory);

                var imageKeys = KeysToString(data.ImageShortcutKeys);
                var gifKeys = KeysToString(data.GifShortcutKeys);

                var effectiveSaveDirectory = string.IsNullOrWhiteSpace(data.SaveDirectory)
                    ? SaveDirectory
                    : data.SaveDirectory;

                var settingsContainer = new SettingsContainer
                {
                    EnableFullscreenCapture = data.EnableFullscreenCapture,
                    EnableGIF = data.EnableGIF,
                    SaveAllImages = data.SaveAllImages,
                    EnablePrintScreen = data.EnablePrintScreen,
                    EnableSound = data.EnableSound,
                    SaveDirectory = effectiveSaveDirectory,
                    EnableImageShortcut = data.EnableImageShortcut,
                    EnableGIFShortcut = data.EnableGIFShortcut,
                    ImageKeys = imageKeys,
                    GifKeys = gifKeys
                };

                var jsonStr = JsonConvert.SerializeObject(settingsContainer);

                File.WriteAllText(settingsFile, JToken.Parse(jsonStr).ToString());
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Logging.Log("Cannot save settings - access denied to settings folder", uaEx);
            }
            catch (IOException ioEx)
            {
                Logging.Log("Cannot save settings - file I/O error", ioEx);
            }
            catch (JsonException jsonEx)
            {
                Logging.Log("Cannot save settings - JSON serialization error", jsonEx);
            }
            catch (Exception ex)
            {
                Logging.Log($"Unexpected error saving settings: {ex.GetType().Name}", ex);
            }
        }
        
        private void UpdateSettings()
        {
            var settingsFile = Constants.SETTINGS_FILE;

            try
            {
                var jsonStr = File.ReadAllText(settingsFile);

                var configContainer = JsonConvert.DeserializeObject<SettingsContainer>(jsonStr) ?? new SettingsContainer
                {
                    EnableFullscreenCapture = EnableFullscreenCapture,
                    EnableGIF = EnableGIF,
                    SaveAllImages = SaveAllImages,
                    SaveDirectory = string.IsNullOrWhiteSpace(SaveDirectory) ? Constants.DEFAULT_ALL_IMAGES_FOLDER : SaveDirectory,
                    EnableImageShortcut = EnableImageShortcut,
                    EnableGIFShortcut = EnableGIFShortcut,
                    ImageKeys = CaptureImageShortcut,
                    GifKeys = CaptureGIFShortcut,
                    EnablePrintScreen = EnablePrintScreen,
                    EnableSound = EnableSound
                };

                EnableFullscreenCapture = configContainer.EnableFullscreenCapture;
                EnableGIF = configContainer.EnableGIF;
                SaveAllImages = configContainer.SaveAllImages;
                EnablePrintScreen = configContainer.EnablePrintScreen;
                EnableSound = configContainer.EnableSound;
                SaveDirectory = !string.IsNullOrWhiteSpace(configContainer.SaveDirectory) ? configContainer.SaveDirectory : "";
                EnableImageShortcut = configContainer.EnableImageShortcut;
                CaptureImageShortcut = !string.IsNullOrWhiteSpace(configContainer.ImageKeys) ? configContainer.ImageKeys : "";
                _captureImageShortcutKeys = !string.IsNullOrWhiteSpace(configContainer.ImageKeys) ? StringToKeys(configContainer.ImageKeys) : new List<Key>();
                EnableGIFShortcut = configContainer.EnableGIFShortcut;
                CaptureGIFShortcut = !string.IsNullOrWhiteSpace(configContainer.GifKeys) ? configContainer.GifKeys : "";
                _captureGIFShortcutKeys = !string.IsNullOrWhiteSpace(configContainer.GifKeys) ? StringToKeys(configContainer.GifKeys) : new List<Key>();
            }
            catch (FileNotFoundException)
            {
                Logging.Log("Settings file not found. Using default values.");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Logging.Log("Cannot read settings file - access denied", uaEx);
            }
            catch (IOException ioEx)
            {
                Logging.Log("Settings file I/O error - deleting corrupted file", ioEx);
                TryDeleteCorruptedSettingsFile(settingsFile);
                RecreateDefaultSettings();
            }
            catch (JsonException jsonEx)
            {
                Logging.Log("Settings file is corrupted (invalid JSON) - deleting file", jsonEx);
                TryDeleteCorruptedSettingsFile(settingsFile);
                RecreateDefaultSettings();
            }
            catch (Exception ex)
            {
                Logging.Log($"Unexpected error reading settings: {ex.GetType().Name}", ex);
                TryDeleteCorruptedSettingsFile(settingsFile);
                RecreateDefaultSettings();
            }
        }

        private void TryDeleteCorruptedSettingsFile(string settingsFile)
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    File.Delete(settingsFile);
                }
            }
            catch (Exception ex)
            {
                Logging.Log("Could not delete corrupted settings file", ex);
            }
        }

        private void RecreateDefaultSettings()
        {
            SaveSettings(new SettingsData(
                EnableFullscreenCapture, EnableGIF, SaveAllImages, SaveDirectory, EnableImageShortcut,
                CaptureImageShortcutKeys, EnableGIFShortcut, CaptureGIFShortcutKeys, EnablePrintScreen, EnableSound));
        }

        private static List<Key> StringToKeys(string text)
        {
            var keyConverter = new KeyConverter();

            var split = text.Split(' ');

            return split.Select(s => keyConverter.ConvertFromString(s))
                .Where(x => x != null)
                .Select(x => (Key) x)
                .ToList();
        }

        private static string KeysToString(IReadOnlyCollection<Key> keys)
        {
            var keyConverter = new KeyConverter();

            if (keys.Count == 0)
                return "";

            var allKeys = keys.Aggregate("", (current, key) => current + keyConverter.ConvertToString(key) + " ");
            allKeys = allKeys.Substring(0, allKeys.Length - 1);

            return allKeys;
        }
        
        // ReSharper disable once UnusedMember.Local
        private static List<string> GetListFromString(string text)
        {
            var split = text.Split(' ');

            return split.ToList();
        }

        // ReSharper disable once UnusedMember.Local
        private string GetStringFromKeys(IReadOnlyCollection<string> keys)
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
        public bool EnableFullscreenCapture { get; set; }

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
