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
using Gma.System.MouseKeyHook;
using ScreenShot.src.capture;
using ScreenShot.src.settings;
using ScreenShot.src.tools;
using ScreenShot.src.tools.display;
using ScreenShot.src.tools.gpu;
using ScreenShot.views.windows;
using static ScreenShot.src.tools.util.URLUtils;

namespace ScreenShot.views
{
    public partial class App
    {
        private const bool CAPTURE_TESTING = true;
        
        private static int isAlreadyCapturingScreen;
        
        private NotifyIcon taskbarIcon;

        private readonly Settings settings = new();
        private readonly Config config = new();

        public App()
        {
            InitializeCEFSettings();
            InitializeComponent();

            ConfigureTaskbar();
            ConfigureShortcuts();
            
            WindowHistory.BeginObservingWindows();
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
            if (!settings.CaptureImageShortcutKeys.Any() && !settings.CaptureGIFShortcutKeys.Any()) return;
            
            var imageCombination = WPFKeysToFormsKeyCombination(settings.CaptureImageShortcutKeys);
            var gifCombination = WPFKeysToFormsKeyCombination(settings.CaptureGIFShortcutKeys);

            var dictionary = new Dictionary<Combination, Action>();
            if (imageCombination != null)
            {
                dictionary[imageCombination] = HandleCaptureImageShortcut;
            }
            
            if (gifCombination != null)
            {
                dictionary[gifCombination] = TryInstantiateCapture<CaptureGIF>;
            }
            
            Hook.GlobalEvents().OnCombination(dictionary);
        }
        
        private void HandleCaptureImageShortcut()
        {
            var hwnd = WindowHistory.APPLICATION_HISTORY.Last.HWND;

            var isGameWindow = GraphicsUtil.IsFullscreenGameWindow(hwnd);
            if (isGameWindow == null)
            {
                TryInstantiateCapture<CaptureImage>();
                return;
            }

            CaptureScreenDirectX.Capture(hwnd);
        }

        private void TryInstantiateCapture<T>() where T : src.capture.Capture
        {
            if (isAlreadyCapturingScreen > 0) return;

            Interlocked.Increment(ref isAlreadyCapturingScreen);
            
            CheckOAuth2(() =>
            {
                var captureGeneric = (T)Activator.CreateInstance(typeof(T), settings, config);

                captureGeneric.Completed += (_, _) => Interlocked.Decrement(ref isAlreadyCapturingScreen);
                captureGeneric.Show();
            });
        }

        private void ConfigureTaskbar()
        {
            var strip = new ContextMenuStrip();

            var menuCaptureImage = new ToolStripMenuItem("Capture Image", null, (_, _) => TryInstantiateCapture<CaptureImage>());
            var menuCaptureGIF = new ToolStripMenuItem("Capture GIF", null, (_, _) => TryInstantiateCapture<CaptureGIF>());

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
                        TryInstantiateCapture<CaptureImage>();
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

        private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            taskbarIcon.Dispose();
        }

        private void CheckOAuth2(Action callback)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!config.EnableOAuth2 || CAPTURE_TESTING)
            {
                callback();
                return;
            }

            // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable 162
            CheckIfOAuth2CredentialsValid(config, callback);
#pragma warning restore 162
        }

        private static Combination WPFKeysToFormsKeyCombination(IReadOnlyCollection<Key> keys)
        {
            if (!keys.Any()) return null;
            
            var wpfKeys = keys.Select(KeyInterop.VirtualKeyFromKey)
                .Select(x => (Keys) x)
                .Select(x => x.ToString())
                .ToList();

            var wpfString = string.Join("+", wpfKeys);
            var combination = Combination.FromString(wpfString);
            
            return combination;
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
