using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Gma.System.MouseKeyHook;
using Newtonsoft.Json;
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
        private Config config;

        public App()
        {
            InitializeComponent();

            Debug.WriteLine($"[APP] Application starting, dev mode: {isDevMode}");

            try
            {
                Debug.WriteLine($"[APP] Initializing Config...");
                config = new Config();
                Debug.WriteLine($"[APP] Config initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] ✗ Config initialization failed: {ex.GetType().Name}");
                Debug.WriteLine($"[APP] Error: {ex.Message}");
                Debug.WriteLine($"[APP] Stack trace: {ex.StackTrace}");
                Logging.Log($"Config initialization failed: {ex.Message}");
                config = null;
            }

            ConfigureTaskbar();
            ConfigureShortcuts();

            WindowHistory.BeginObservingWindows();

            // Show settings window on first run if server is not configured
            if (string.IsNullOrWhiteSpace(config.Server))
            {
                Logging.Log($"Server endpoint not configured. Please input your server's image upload host location. An example php host file is located at {Constants.GITHUB}.");

                var settingsWindow = new SettingsWindow(settings, config);
                settingsWindow.Show();
            }

            Debug.WriteLine($"[APP] Configured server: {config.Server}");

            // Run OAuth once at startup if server is configured but no token exists
            if (!string.IsNullOrWhiteSpace(config.Server) && string.IsNullOrWhiteSpace(config.OAuth2Token))
            {
                Debug.WriteLine($"[APP] Server configured but no OAuth token - running authentication");
                CheckOAuth2(() => { });
            }
        }

        private void ConfigureShortcuts()
        {
            if (!settings.CaptureImageShortcutKeys.Any() && !settings.CaptureGIFShortcutKeys.Any())
            {
                Debug.WriteLine("[SHORTCUT] No shortcut keys configured");
                return;
            }

            Debug.WriteLine($"[SHORTCUT] Image shortcut keys: {string.Join("+", settings.CaptureImageShortcutKeys)}");
            Debug.WriteLine($"[SHORTCUT] GIF shortcut keys: {string.Join("+", settings.CaptureGIFShortcutKeys)}");

            var imageCombination = WPFKeysToFormsKeyCombination(settings.CaptureImageShortcutKeys);
            var gifCombination = WPFKeysToFormsKeyCombination(settings.CaptureGIFShortcutKeys);

            var dictionary = new Dictionary<Combination, Action>();
            if (imageCombination != null)
            {
                dictionary[imageCombination] = HandleCaptureImageShortcut;
                Debug.WriteLine($"[SHORTCUT] Registered image capture shortcut: {imageCombination}");
            }

            if (gifCombination != null)
            {
                dictionary[gifCombination] = TryInstantiateCapture<CaptureGIF>;
                Debug.WriteLine($"[SHORTCUT] Registered GIF capture shortcut: {gifCombination}");
            }

            Hook.GlobalEvents().OnCombination(dictionary);
            Debug.WriteLine("[SHORTCUT] Global hook registered");
        }
        
        private async void HandleCaptureImageShortcut()
        {
            Debug.WriteLine("[SHORTCUT] HandleCaptureImageShortcut triggered");

            // Don't capture if settings window is open
            foreach (var window in System.Windows.Application.Current.Windows)
            {
                if (window is SettingsWindow)
                {
                    Debug.WriteLine("[SHORTCUT] Aborting: Settings window is open");
                    return;
                }
            }

            var windowHistoryItem = WindowHistory.APPLICATION_HISTORY.Last;

            if (windowHistoryItem == null)
            {
                Debug.WriteLine("[SHORTCUT] Aborting: No window in history");
                return;
            }

            Debug.WriteLine("[SHORTCUT] Proceeding with capture");

            var hwnd = windowHistoryItem.HWND;
            var isGameWindow = GraphicsUtil.IsFullscreenGameWindow(hwnd);
            Debug.WriteLine($"[SHORTCUT] IsFullscreenGameWindow: {isGameWindow}");

            // For true exclusive fullscreen games, capture directly
            if (isGameWindow != null)
            {
                Debug.WriteLine("[SHORTCUT] Exclusive fullscreen game detected, capturing entire window");
                var bitmap = await CaptureScreenDirectX.CaptureFullWindow(hwnd);
                if (bitmap != null)
                {
                    var file = Path.GetTempPath() + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
                    bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                    bitmap.Dispose();

                    if (settings.SaveAllImages)
                    {
                        var output = settings.SaveDirectory + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".png";
                        File.Move(file, output);
                        file = output;
                    }

                    Upload.UploadFile(file, settings, config);
                    Debug.WriteLine("[SHORTCUT] Fullscreen capture uploaded");
                }
                return;
            }

            Debug.WriteLine("[SHORTCUT] Not exclusive fullscreen, using regular capture UI");
            TryInstantiateCapture<CaptureImage>();
        }

        private void TryInstantiateCapture<T>() where T : src.capture.Capture
        {
            if (isAlreadyCapturingScreen > 0) return;

            Interlocked.Increment(ref isAlreadyCapturingScreen);

            var captureGeneric = (T)Activator.CreateInstance(typeof(T), settings, config);

            captureGeneric.Completed += (_, _) => Interlocked.Decrement(ref isAlreadyCapturingScreen);
            captureGeneric.Show();
        }

        private void ConfigureTaskbar()
        {
            var strip = new ContextMenuStrip();

            var menuCaptureImage = new ToolStripMenuItem("Capture Image", null, (_, _) => TryInstantiateCapture<CaptureImage>());
            var menuCaptureGIF = new ToolStripMenuItem("Capture GIF", null, (_, _) => TryInstantiateCapture<CaptureGIF>());

            var menuUploadImageManually = new ToolStripMenuItem("Upload Image", null, (_, _) =>
            {
                var file = FileUtils.BrowseForFile("Images (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif");
                
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

            var menuClearOverlay = new ToolStripMenuItem("Clear Overlay Windows", null, (_, _) =>
            {
                CaptureScreenDirectX.ClearOverlayWindows();
            });

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
                menuClearOverlay,
                menuSettings,
                new ToolStripSeparator(),
                menuEnableDevMode,
                new ToolStripSeparator(),
                menuExit
            });

            strip.Items[0].Font = new Font(strip.Items[0].Font, strip.Items[0].Font.Style | System.Drawing.FontStyle.Bold);
            strip.Items[1].Font = new Font(strip.Items[1].Font, strip.Items[1].Font.Style | System.Drawing.FontStyle.Bold);

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
            CaptureScreenDirectX.ClearOverlayWindows();
            taskbarIcon.Dispose();
        }

        private void CheckOAuth2(Action callback)
        {
            Debug.WriteLine("[OAUTH] Checking OAuth2 credentials");
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
            const int OAUTH_CALLBACK_PORT = 52805;
            var localCallbackUrl = $"http://{IPAddress.Loopback}:{OAUTH_CALLBACK_PORT}/";

            // Get base server URL (remove /api/ss if present)
            var baseServer = config.Server.TrimEnd('/');
            if (baseServer.EndsWith("/api/ss"))
            {
                baseServer = baseServer.Substring(0, baseServer.Length - "/api/ss".Length);
            }

            var fullURL = JoinURL(baseServer, Constants.API_ENDPOINT_IS_AUTHORIZED);
            fullURL = $"{fullURL}?redirect_uri={Uri.EscapeDataString(localCallbackUrl)}";

            Debug.WriteLine($"[OAUTH] Checking credentials at: {fullURL}");

            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(fullURL),
                Method = HttpMethod.Get
            };

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
                Debug.WriteLine($"[OAUTH] Response status: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAUTH] Connection error: {ex.Message}");
                Logging.Log("Could not connect to authentication endpoint. Exiting...");
                Current.Shutdown(-1);
                return;
            }

            var status = (int)response.StatusCode;

            // Check if already authenticated (cookies received)
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Debug.WriteLine("[OAUTH] Already authenticated - session cookie set");
                response.Dispose();
                Current.Dispatcher.Invoke(callback);
                return;
            }

            // Check if redirect to OAuth provider needed
            if (status is >= 300 and <= 399)
            {
                Debug.WriteLine("[OAUTH] Need to authenticate - server redirecting to OAuth");
                var redirectUri = response.Headers.Location?.ToString();
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    Debug.WriteLine("[OAUTH] Redirect but no Location header");
                    Logging.Log("OAuth redirect without Location header");
                    response.Dispose();
                    return;
                }

                // Convert relative URLs to absolute
                if (!redirectUri.StartsWith("http://") && !redirectUri.StartsWith("https://"))
                {
                    redirectUri = config.Server.TrimEnd('/') + redirectUri;
                }

                Debug.WriteLine($"[OAUTH] Opening browser to: {redirectUri}");
                response.Dispose();

                // Prepare local listener for callback
                var http = new HttpListener();
                http.Prefixes.Add(localCallbackUrl);
                http.Start();
                Debug.WriteLine($"[OAUTH] Listening for callback at: {localCallbackUrl}");

                try
                {
                    Process.Start(redirectUri);
                    Debug.WriteLine("[OAUTH] Browser opened - user authenticating");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OAUTH] Failed to open browser: {ex.Message}");
                    Logging.Log($"Failed to open browser for OAuth: {ex.Message}");
                    http.Stop();
                    return;
                }

                try
                {
                    var context = await http.GetContextAsync();
                    Debug.WriteLine($"[OAUTH] ✓ Received callback at local listener");

                    // Extract token from query parameters
                    var token = context.Request.QueryString["token"];
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        Debug.WriteLine($"[OAUTH] Extracted token from callback");
                        config.OAuth2Token = token;
                        Debug.WriteLine($"[OAUTH] Token saved to config");
                    }
                    else
                    {
                        Debug.WriteLine("[OAUTH] No token in callback URL");
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Authentication Successful</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        .container {
            text-align: center;
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
        }
        .checkmark {
            font-size: 48px;
            margin-bottom: 20px;
        }
        h1 {
            color: #333;
            margin: 0 0 10px 0;
            font-size: 24px;
        }
        p {
            color: #666;
            margin: 10px 0 0 0;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='checkmark'>✓</div>
        <h1>Authentication Successful</h1>
        <p>You can close this window now.</p>
    </div>
</body>
</html>";
                    var buffer = Encoding.UTF8.GetBytes(html);
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                }
                finally
                {
                    http.Stop();
                }

                Current.Dispatcher.Invoke(callback);
            }
            else
            {
                Debug.WriteLine($"[OAUTH] Unexpected response status: {response.StatusCode}");
                Logging.Log($"Unexpected OAuth response: {response.StatusCode}");
                response.Dispose();
                Current.Shutdown();
            }
        }
    }
}
