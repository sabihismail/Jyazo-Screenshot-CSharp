using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using ScreenShot.src;
using ScreenShot.src.capture;
using ScreenShot.src.tools;
using ScreenShot.src.upload;

namespace ScreenShot.views
{
    public partial class App
    {
        private NotifyIcon taskbarIcon;

        private readonly Settings settings = new Settings();
        private readonly Config config = new Config();

        private GlobalKeyboardHook globalKeyboardHook;

        private HashSet<Key> imagePressed = new HashSet<Key>();
        private int imageActive;

        private HashSet<Key> gifPressed = new HashSet<Key>();
        private int gifActive;

        public App()
        {
            InitializeCEFSettings();
            InitializeComponent();

            ConfigureTaskbar();
            ConfigureShortcuts();
            WindowInformation.BeginObservingWindows();
        }

        private void InitializeCEFSettings()
        {
            CefSettings cefSettings = new CefSettings
            {
                UserAgent = Constants.USER_AGENT,
            };

            Cef.Initialize(cefSettings);
        }

        private void ConfigureShortcuts()
        {
            if (settings.CaptureImageShortcutKeys.Count == 0 && settings.CaptureGIFShortcutKeys.Count == 0) return;

            globalKeyboardHook = new GlobalKeyboardHook();
            globalKeyboardHook.KeyboardReleased += (sender, e) =>
            {
                var key = KeyInterop.KeyFromVirtualKey(e.KeyboardData.VirtualCode);

                imagePressed.Remove(key);
                gifPressed.Remove(key);
            };
            globalKeyboardHook.KeyboardPressed += (sender, e) =>
            {
                var key = KeyInterop.KeyFromVirtualKey(e.KeyboardData.VirtualCode);

                if (CheckKeyboardShortcut(key, settings.CaptureImageShortcutKeys, ref imagePressed))
                {
                    TryInstatiateCaptureImage();
                }

                if (CheckKeyboardShortcut(key, settings.CaptureGIFShortcutKeys, ref gifPressed))
                {
                    TryInstatiateCaptureGIF();
                }
            };
        }

        private void TryInstatiateCaptureImage()
        {
            if (imageActive != 0) return;

            var captureImage = new CaptureImage(settings, config);

            Interlocked.Increment(ref imageActive);

            captureImage.Show();

            Interlocked.Decrement(ref imageActive);
        }

        private void TryInstatiateCaptureGIF()
        {
            if (gifActive != 0) return;

            var captureGIF = new CaptureGIF(settings, config);

            Interlocked.Increment(ref gifActive);

            captureGIF.Show();

            Interlocked.Decrement(ref gifActive);
        }

        private static bool CheckKeyboardShortcut(Key key, List<Key> shortcutKeys, ref HashSet<Key> clickedList)
        {
            if (!shortcutKeys.Contains(key)) return false;

            clickedList.Add(key);

            return clickedList.Intersect(shortcutKeys).Count() == shortcutKeys.Count;
        }

        private void ConfigureTaskbar()
        {
            var strip = new ContextMenuStrip();

            var menuCaptureImage = new ToolStripMenuItem("Capture Image", null, (sender, args) =>
            {
                void action()
                {
                    var captureImage = new CaptureImage(settings, config);

                    captureImage.Show();
                }

                CheckOAuth2(action);
            });

            var menuCaptureGIF = new ToolStripMenuItem("Capture GIF", null, (sender, args) =>
            {
                void action()
                {
                    var captureGIF = new CaptureGIF(settings, config);

                    captureGIF.Show();
                }

                CheckOAuth2(action);
            });

            var menuViewAllImages = new ToolStripMenuItem("View All Images", null, (sender, args) =>
            {
                if (!settings.SaveAllImages) return;

                Process.Start("explorer.exe", settings.SaveDirectory);
            });

            var menuSettings = new ToolStripMenuItem("Settings", null, (sender, args) =>
            {
                var settingsWindow = new SettingsWindow(settings, config);

                settingsWindow.Show();
            });

            var menuExit = new ToolStripMenuItem("Exit", null, (sender, args) =>
            {
                Shutdown();
            });

            strip.Items.Add(menuCaptureImage);
            strip.Items.Add(menuCaptureGIF);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(menuViewAllImages);
            strip.Items.Add(menuSettings);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(menuExit);

            strip.Items[0].Font = new Font(strip.Items[0].Font, strip.Items[0].Font.Style | System.Drawing.FontStyle.Bold);
            strip.Items[1].Font = new Font(strip.Items[1].Font, strip.Items[1].Font.Style | System.Drawing.FontStyle.Bold);

            taskbarIcon = new NotifyIcon
            {
                ContextMenuStrip = strip
            };

            taskbarIcon.MouseClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    menuCaptureImage.Text = $"Capture Image ({settings.CaptureImageShortcut.Replace(" ", " + ")})";
                    menuCaptureGIF.Text = $"Capture GIF ({settings.CaptureGIFShortcut.Replace(" ", " + ")})";
                }
                else if (args.Button == MouseButtons.Left)
                {
                    void action()
                    {
                        var captureImage = new CaptureImage(settings, config);

                        captureImage.Show();
                    }

                    CheckOAuth2(action);
                }
            };

            using (var stream = GetResourceStream(new Uri("/resources/icon.ico", UriKind.Relative))?.Stream)
            {
                taskbarIcon.Icon = new Icon(stream ?? throw new InvalidOperationException("icon not found"));
            }

            taskbarIcon.Visible = true;
        }

        private void CheckOAuth2(Action callback)
        {
            if (!config.EnableOAuth2)
            {
                callback();
                return;
            }

            WebBrowserUtil.CheckIfOAuth2CredentialsValid(config, callback);
        }
    }
}
