using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Gma.System.MouseKeyHook;
using Newtonsoft.Json.Linq;
using ScreenShot.src.capture;
using ScreenShot.src.settings;
using ScreenShot.src.tools;
using ScreenShot.src.tools.display;
using ScreenShot.src.tools.gpu;
using ScreenShot.src.tools.util;
using ScreenShot.src.upload;
using ScreenShot.views.windows;
using static ScreenShot.src.tools.util.URLUtils;

namespace ScreenShot.views
{
    public partial class App
    {
        public static int isDevMode;
        private static int isAlreadyCapturingScreen;
        
        private NotifyIcon taskbarIcon;

        private readonly Settings settings = new();
        private readonly Config config = new();

        public App()
        {
            InitializeComponent();

            ConfigureTaskbar();
            ConfigureShortcuts();
            
            WindowHistory.BeginObservingWindows();
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
            var windowHistoryItem = WindowHistory.APPLICATION_HISTORY.Last;
                
            if (windowHistoryItem == null) return;
                    
            var hwnd = windowHistoryItem.HWND;
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

            var menuUploadImageManually = new ToolStripMenuItem("Upload Image", null, (_, _) =>
            {
                var file = FileUtils.BrowseForFile("Images (*.png)|*.png");
                
                if (string.IsNullOrWhiteSpace(file)) return;
                
                Upload.UploadFile(file, settings, config);
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

            var menuEnableDevMode = new ToolStripMenuItem("Enable Dev Mode", null, (obj, args) =>
            {
                if (((ToolStripMenuItem)obj).Checked)
                {
                    Interlocked.Increment(ref isDevMode);
                }
                else
                {
                    Interlocked.Decrement(ref isDevMode);
                }
            });
            menuEnableDevMode.CheckOnClick = true;

            var menuExit = new ToolStripMenuItem("Exit", null, (_, _) =>
            {
                Shutdown();
            });

            strip.Items.AddRange(new ToolStripItem[]
            {
                menuCaptureImage,
                menuCaptureGIF,
                new ToolStripSeparator(),
                menuUploadImageManually,
                new ToolStripSeparator(),
                menuViewAllImages,
                menuSettings,
                new ToolStripSeparator(),
                menuEnableDevMode,
                new ToolStripSeparator(),
                menuExit
            });

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
            if (!config.EnableOAuth2 || isDevMode == 1)
            {
                callback();
                return;
            }

            CheckIfOAuth2CredentialsValid(config, callback);
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

        // OAuth2 Flow based on https://github.com/googlesamples/oauth-apps-for-windows/blob/master/OAuthDesktopApp/OAuthDesktopApp/MainWindow.xaml.cs
        // ReSharper disable once UnusedMember.Local
        public static async void CheckIfOAuth2CredentialsValid(Config config, Action callback)
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
                response.Dispose();

                var redirectUrl = $"http://{IPAddress.Loopback}:{GetRandomUnusedPort()}/";

                var http = new HttpListener();
                http.Prefixes.Add(redirectUrl);
                http.Start();

                var completeURL = $"{fullURL}?redirect_uri={Uri.EscapeDataString(redirectUrl)}";

                Process.Start(completeURL);

                var context = await http.GetContextAsync();

                var contextResponse = context.Response;
                var baseURL = config.Server.Substring(0, config.Server.IndexOf("/", "https://".Length, StringComparison.Ordinal));
                var buffer = Encoding.UTF8.GetBytes($"<html><head><meta http-equiv='refresh' content='10;url='{baseURL}'></head><body>Please return to the app.</body></html>");
                contextResponse.ContentLength64 = buffer.Length;
                var responseOutput = contextResponse.OutputStream;
                await responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith(_ =>
                {
                    responseOutput.Close();
                    http.Stop();
                });

                if (context.Request.QueryString.Get("error") != null)
                {
                    Logging.Log($"OAuth authorization error: {context.Request.QueryString.Get("error")}");
                    return;
                }

                if (context.Request.QueryString.Get("cookies") == null)
                {
                    Logging.Log($"Malformed authorization response: {context.Request.QueryString.Get("cookies")}");
                    return;
                }

                var cookiesStr = Uri.UnescapeDataString(context.Request.QueryString.Get("cookies"));
                var cookiesDynamic = JObject.Parse(cookiesStr);

                var domain = baseURL.Substring(baseURL.IndexOf("//", StringComparison.InvariantCulture) + "//".Length);
                if (domain.Contains(":"))
                {
                    domain = domain.Substring(0, domain.IndexOf(":", StringComparison.InvariantCulture));
                }
                
                var cookies = new List<Cookie>();
                foreach (var cookie in cookiesDynamic)
                {
                    cookies.Add(new Cookie(cookie.Key, cookie.Value?.ToString(), "/", domain));
                }

                config.SetOAuth2Cookies(cookies);

                Current.Dispatcher.Invoke(callback);
            }
            else
            {
                Logging.Log("Unsupported non redirect OAuth2 endpoint.");
                Current.Shutdown();
            }
        }
    }
}
