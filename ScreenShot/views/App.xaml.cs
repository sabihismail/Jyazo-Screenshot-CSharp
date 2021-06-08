using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using ScreenShot.src.capture;
using ScreenShot.src.settings;
using ScreenShot.src.tools;
using ScreenShot.src.tools.display;
using ScreenShot.src.tools.hooks;
using ScreenShot.views.windows;
using static ScreenShot.src.tools.util.URLUtils;

namespace ScreenShot.views
{
    public partial class App
    {
        private NotifyIcon taskbarIcon;

        private readonly Settings settings = new();
        private readonly Config config = new();

        private GlobalKeyboardHook globalKeyboardHook;

        private HashSet<Key> imagePressed = new();
        private int imageActive;

        private HashSet<Key> gifPressed = new();
        private int gifActive;

        public App()
        {
            InitializeCEFSettings();
            InitializeComponent();

            ConfigureTaskbar();
            ConfigureShortcuts();

            WindowInformation.BeginObservingWindows();
        }

        private static void InitializeCEFSettings()
        {
            var cefSettings = new CefSettings
            {
                UserAgent = Constants.USER_AGENT
            };

            Cef.Initialize(cefSettings);
        }

        private void ConfigureShortcuts()
        {
            if (settings.CaptureImageShortcutKeys.Count == 0 && settings.CaptureGIFShortcutKeys.Count == 0) return;

            globalKeyboardHook = new GlobalKeyboardHook();
            globalKeyboardHook.KeyboardReleased += (_, e) =>
            {
                var key = KeyInterop.KeyFromVirtualKey(e.KeyboardData.VirtualCode);

                imagePressed.Remove(key);
                gifPressed.Remove(key);
            };

            globalKeyboardHook.KeyboardPressed += (_, e) =>
            {
                var key = KeyInterop.KeyFromVirtualKey(e.KeyboardData.VirtualCode);

                if (CheckKeyboardShortcut(key, settings.CaptureImageShortcutKeys, ref imagePressed))
                {
                    TryInstantiateCaptureImage();
                }

                if (CheckKeyboardShortcut(key, settings.CaptureGIFShortcutKeys, ref gifPressed))
                {
                    TryInstantiateCaptureGIF();
                }
            };
        }

        private void TryInstantiateCaptureImage()
        {
            if (imageActive != 0) return;

            var captureImage = new CaptureImage(settings, config);

            Interlocked.Increment(ref imageActive);

            captureImage.Show();

            Interlocked.Decrement(ref imageActive);
        }

        private void TryInstantiateCaptureGIF()
        {
            if (gifActive != 0) return;

            var captureGIF = new CaptureGIF(settings, config);

            Interlocked.Increment(ref gifActive);

            captureGIF.Show();

            Interlocked.Decrement(ref gifActive);
        }

        private static bool CheckKeyboardShortcut(Key key, ICollection<Key> shortcutKeys, ref HashSet<Key> clickedList)
        {
            if (!shortcutKeys.Contains(key)) return false;

            clickedList.Add(key);

            return clickedList.Intersect(shortcutKeys).Count() == shortcutKeys.Count;
        }

        private void ConfigureTaskbar()
        {
            var strip = new ContextMenuStrip();

            var menuCaptureImage = new ToolStripMenuItem("Capture Image", null, (_, _) =>
            {
                void Action()
                {
                    var captureImage = new CaptureImage(settings, config);

                    captureImage.Show();
                }

                CheckOAuth2(Action);
            });

            var menuCaptureGIF = new ToolStripMenuItem("Capture GIF", null, (_, _) =>
            {
                void Action()
                {
                    var captureGIF = new CaptureGIF(settings, config);

                    captureGIF.Show();
                }

                CheckOAuth2(Action);
            });

            var menuViewAllImages = new ToolStripMenuItem("View All Images", null, (_, _) =>
            {
                if (!settings.SaveAllImages) return;

                Process.Start("explorer.exe", settings.SaveDirectory);
            })
            {
                Enabled = settings.SaveAllImages
            };

            var menuSettings = new ToolStripMenuItem("Settings", null, (_, _) =>
            {
                var settingsWindow = new SettingsWindow(settings, config);

                settingsWindow.Show();
            });

            var menuExit = new ToolStripMenuItem("Exit", null, (_, _) =>
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

            strip.Items[0].Font = new Font(strip.Items[0].Font, strip.Items[0].Font.Style | FontStyle.Bold);
            strip.Items[1].Font = new Font(strip.Items[1].Font, strip.Items[1].Font.Style | FontStyle.Bold);

            taskbarIcon = new NotifyIcon
            {
                ContextMenuStrip = strip
            };

            taskbarIcon.MouseClick += (_, args) =>
            {
                switch (args.Button)
                {
                    case MouseButtons.Right:
                        var imageShortcutText = settings.EnableImageShortcut
                            ? settings.CaptureImageShortcut.Replace(" ", " + ")
                            : ScreenShot.Properties.Resources.App_ConfigureTaskbar_Capture_Disabled;
                        
                        var gifShortcutText = settings.EnableGIFShortcut
                            ? settings.CaptureGIFShortcut.Replace(" ", " + ")
                            : ScreenShot.Properties.Resources.App_ConfigureTaskbar_Capture_Disabled;

                        menuCaptureImage.Text =  string.Format(ScreenShot.Properties.Resources.App_ConfigureTaskbar_Capture_Image, imageShortcutText);
                        menuCaptureGIF.Text  = string.Format(ScreenShot.Properties.Resources.App_ConfigureTaskbar_Capture_GIF, gifShortcutText);
                        break;
                    
                    case MouseButtons.Left:
                    {
                        void Action()
                        {
                            var captureImage = new CaptureImage(settings, config);

                            captureImage.Show();
                        }

                        CheckOAuth2(Action);
                        break;
                    }
                    
                    case MouseButtons.None:
                        break;
                    
                    case MouseButtons.Middle:
                        break;
                    
                    case MouseButtons.XButton1:
                        break;
                    
                    case MouseButtons.XButton2:
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
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

            CheckIfOAuth2CredentialsValid(config, callback);
        }

        private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            taskbarIcon.Dispose();
        }

        private static async void CheckIfOAuth2CredentialsValid(Config config, Action callback)
        {
            var fullURL = JoinURL(config.Server, Constants.API_ENDPOINT_IS_AUTHORIZED);

            using var client = new CookieHttpClient(config, false);
            
            var uri = new Uri(fullURL);
            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = HttpMethod.Get
            };

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch
            {
                Logging.Log("Could not connect to " + request + ". Exiting...");
                Current.Shutdown(-1);

                return;
            }

            var status = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Current.Dispatcher.Invoke(callback);
                return;
            }

            if (status is >= 300 and <= 399)
            {
                var redirectUri = response.Headers.Location;

                response.Dispose();

                string redirect;
                if (redirectUri.IsAbsoluteUri)
                {
                    redirect = redirectUri.AbsoluteUri;
                }
                else
                {
                    redirect = uri.GetLeftPart(UriPartial.Authority) + redirectUri.OriginalString;
                }

                var browserWindow = new WebBrowserWindow(redirect, fullURL);

                browserWindow.Closed += (_, _) =>
                {
                    var host = uri.Host;

                    var cookies = browserWindow.CookiesDotNet
                        .FindAll(x => x.Domain.Contains(host));

                    config.SetOAuth2Cookies(cookies);

                    Current.Dispatcher.Invoke(callback);
                };

                browserWindow.Show();
            }
            else
            {
                Logging.Log("Unsupported non redirect OAuth2 endpoint.");
                Current.Shutdown();
            }
        }
    }
}
