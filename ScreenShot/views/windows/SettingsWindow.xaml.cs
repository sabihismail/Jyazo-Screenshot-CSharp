using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ScreenShot.src.settings;
using ScreenShot.src.tools.util;

namespace ScreenShot.views.windows
{
    public partial class SettingsWindow
    {
        private readonly Settings settings;
        private readonly Config config;

        private static bool imageShortcutSelected;
        private static bool gifShortcutSelected;
        private static List<Key> imageShortcutKeycodes = new();
        private static List<Key> gifShortcutKeycodes = new();

        public SettingsWindow(Settings settings, Config config)
        {
            this.settings = settings;
            this.config = config;

            InitializeComponent();

            ChkAutomaticallySaveCapturedImagesToDisk_OnClick(null, null);
            ChkEnableImageShortcut_OnClick(null, null);
            ChkEnableGIFShortcut_OnClick(null, null);

            ChkEnableFullscreenCapture.IsChecked = settings.EnableFullscreenCapture;
            ChkEnableGIFCapture.IsChecked = settings.EnableGIF;

            ChkAutomaticallySaveCapturedImagesToDisk.IsChecked = settings.SaveAllImages;
            TxtSaveAllCapturedImages.Text = settings.SaveAllImages ? settings.SaveDirectory : "";

            ChkEnableImageShortcut.IsChecked = settings.EnableImageShortcut;
            ChkEnableGIFShortcut.IsChecked = settings.EnableGIFShortcut;

            imageShortcutKeycodes = settings.CaptureImageShortcutKeys;
            gifShortcutKeycodes = settings.CaptureGIFShortcutKeys;

            UpdateShortcutText(imageShortcutKeycodes, TxtImageShortcut);
            UpdateShortcutText(gifShortcutKeycodes, TxtGIFShortcut);

            ChkEnablePrintScreen.IsChecked = settings.EnablePrintScreen;
            ChkPlaySound.IsChecked = settings.EnableSound;
        }

        private void ChkAutomaticallySaveCapturedImagesToDisk_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsChecked(ChkAutomaticallySaveCapturedImagesToDisk))
            {
                TxtSaveAllCapturedImages.IsEnabled = true;
                BtnChooseSaveDirectory.IsEnabled = true;
            }
            else
            {
                TxtSaveAllCapturedImages.IsEnabled = false;
                TxtSaveAllCapturedImages.Text = "";
                BtnChooseSaveDirectory.IsEnabled = false;
            }
        }

        private void BtnChooseSaveDirectory_OnClick(object sender, RoutedEventArgs e)
        {
            var path = FileUtils.BrowseForDirectory();

            TxtSaveAllCapturedImages.Text = path;
        }

        private void ChkEnableImageShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsChecked(ChkEnableImageShortcut))
            {
                TxtImageShortcut.IsEnabled = true;
                BtnClearImageShortcut.IsEnabled = true;
            }
            else
            {
                TxtImageShortcut.IsEnabled = false;
                TxtImageShortcut.Text = "";
                BtnClearImageShortcut.IsEnabled = false;
            }
        }

        private void TxtImageShortcut_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!imageShortcutSelected) return;

            if (imageShortcutKeycodes.Count < 3)
            {
                if (!imageShortcutKeycodes.Contains(e.Key))
                {
                    imageShortcutKeycodes.Add(e.Key);
                }

                if (imageShortcutKeycodes.Count <= 0) return;

                UpdateShortcutText(imageShortcutKeycodes, TxtImageShortcut);
            }
            else
            {
                imageShortcutSelected = false;
            }
        }

        private void BtnClearImageShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            TxtImageShortcut_OnGotKeyboardFocus(null, null);
        }

        private void ChkEnableGIFShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsChecked(ChkEnableGIFShortcut))
            {
                TxtGIFShortcut.IsEnabled = true;
                BtnClearGIFShortcut.IsEnabled = true;
            }
            else
            {
                TxtGIFShortcut.IsEnabled = false;
                TxtGIFShortcut.Text = "";
                BtnClearGIFShortcut.IsEnabled = false;
            }
        }

        private void TxtGIFShortcut_OnKeyDown(object sender, KeyEventArgs e)
        {

            if (!gifShortcutSelected) return;

            if (gifShortcutKeycodes.Count < 3)
            {
                if (!gifShortcutKeycodes.Contains(e.Key))
                {
                    gifShortcutKeycodes.Add(e.Key);
                }

                if (gifShortcutKeycodes.Count == 0) return;

                UpdateShortcutText(gifShortcutKeycodes, TxtGIFShortcut);
            }
            else
            {
                gifShortcutSelected = false;
            }
        }

        private static void UpdateShortcutText(IEnumerable<Key> keys, TextBox textBox)
        {
            var k = new KeyConverter();

            var keysString = keys.Select(s => k.ConvertToString(s)).Aggregate("", (current, keyStr) => current + keyStr + " + ");

            if (keysString.Length > 3)
            {
                keysString = keysString.Substring(0, keysString.Length - 3);
            }

            textBox.Text = keysString;
        }
        
        private void BtnClearGIFShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            TxtGIFShortcut_OnGotKeyboardFocus(null, null);
        }

        private void TxtImageShortcut_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            imageShortcutSelected = true;
            imageShortcutKeycodes = new List<Key>();
            TxtImageShortcut.Text = "";
        }

        private void TxtImageShortcut_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            imageShortcutSelected = false;
        }

        private void TxtGIFShortcut_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            gifShortcutSelected = true;
            gifShortcutKeycodes = new List<Key>();
            TxtGIFShortcut.Text = "";
        }

        private void TxtGIFShortcut_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            gifShortcutSelected = false;
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            settings.SaveSettings(IsChecked(ChkEnableFullscreenCapture),
                IsChecked(ChkEnableGIFCapture),
                IsChecked(ChkAutomaticallySaveCapturedImagesToDisk),
                TxtSaveAllCapturedImages.Text.Trim(),
                IsChecked(ChkEnableImageShortcut),
                imageShortcutKeycodes,
                IsChecked(ChkEnableGIFShortcut),
                gifShortcutKeycodes,
                IsChecked(ChkEnablePrintScreen),
                IsChecked(ChkPlaySound));

            Close();
        }

        private void BtnAdvancedSettings_OnClick(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigWindow(config);

            configWindow.Closed += (_, _) =>
            {
                WindowState = WindowState.Normal;
            };

            configWindow.Loaded += (_, _) =>
            {
                WindowState = WindowState.Minimized;
            };

            configWindow.Show();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool IsChecked(ToggleButton chk)
        {
            return chk.IsChecked.HasValue && chk.IsChecked.Value;
        }
    }
}
