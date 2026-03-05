using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using ScreenShot.src.settings;
using ScreenShot.src.tools;
using ScreenShot.src.tools.display;
using ScreenShot.src.tools.util;
using static ScreenShot.src.tools.util.URLUtils;

namespace ScreenShot.src.upload
{
    public static class Upload
    {
        public static async void UploadFile(string file, Settings settings, Config config)
        {
            Debug.WriteLine($"[UPLOAD] Starting upload: file={file}, server={config.Server}, oauth2={config.EnableOAuth2}");

            if (string.IsNullOrWhiteSpace(config.Server) || !config.EnableOAuth2 && string.IsNullOrWhiteSpace(config.ServerPassword))
            {
                Debug.WriteLine("[UPLOAD] Configuration error: missing server or auth");
                Logging.Log("Configuration Error: User inputted server url or server password is invalid.");
                return;
            }

            var result = await UploadToServer(file, config);

            if (string.IsNullOrWhiteSpace(result)) return;

            if (settings.EnableSound)
            {
                PlaySound();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Clipboard.SetDataObject(result);

                try
                {
                    Process.Start(result);
                }
                catch
                {
                    Logging.Log($"Could not open URL: {result}. It was copied to your clipboard anyways.");
                }
            });
        }

        private static async Task<string> UploadToServer(string file, Config config)
        {
            using var client = new HttpClient();

            using var formData = new MultipartFormDataContent();

            var titleText = !string.IsNullOrWhiteSpace(WindowHistory.LastWindowTitle) ? WindowHistory.LastWindowTitle : "";
            var titleContent = new StringContent(titleText, Encoding.UTF8);
            titleContent.Headers.Remove("Content-Type");
            formData.Add(titleContent, "title");

            var streamContent = new StreamContent(File.OpenRead(file));
            streamContent.Headers.Add("Content-Type", FileUtils.GetContentType(Path.GetExtension(file)));
            formData.Add(streamContent, "uploaded_image", Path.GetFileName(file));

            var server = config.Server;
            if (!config.EnableOAuth2)
            {
                Debug.WriteLine("[UPLOAD] Using password authentication");
                server = JoinURL(server, "api/ss");
                if (!string.IsNullOrWhiteSpace(config.ServerPassword))
                {
                    formData.Headers.Add("upload_password", config.ServerPassword);
                    Debug.WriteLine("[UPLOAD] Password header added");
                }
            }
            else
            {
                server = JoinURL(server, "api/ss");
                server = JoinURL(server, Constants.API_ENDPOINT_UPLOAD_SCREENSHOT);
                Debug.WriteLine($"[UPLOAD] Using OAuth2 token authentication, endpoint: {server}");

                if (!string.IsNullOrWhiteSpace(config.OAuth2Token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.OAuth2Token);
                    Debug.WriteLine("[UPLOAD] Bearer token added to Authorization header");
                }
                else
                {
                    Debug.WriteLine("[UPLOAD] OAuth2 enabled but no token available");
                }
            }

            try
            {
                Debug.WriteLine($"[UPLOAD] Posting to: {server}");
                using var httpResponse = await client.PostAsync(server, formData);

                Debug.WriteLine($"[UPLOAD] Response status: {httpResponse.StatusCode}");

                var resultStr = await httpResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[UPLOAD] Response body: {resultStr}");

                var result = JsonConvert.DeserializeObject<ServerResponse>(resultStr);
                if (result == null)
                {
                    Debug.WriteLine("[UPLOAD] Failed to parse response");
                    Logging.Log("No response from server.");
                    return "";
                }

                if (result.Success)
                {
                    Debug.WriteLine($"[UPLOAD] ✓ Upload successful!");
                    Debug.WriteLine($"[UPLOAD] Result: {result.Output}");
                    return result.Output;
                }

                Debug.WriteLine($"[UPLOAD] ✗ Server error: {result.Error}");
                Logging.Log("The server responded with:\n" + result.Error);
                return "";
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[UPLOAD] Exception: {e}");
                Logging.Log($"Could not upload screenShot to URL: {server}.\nError: " + e);
            }

            return "";
        }

        private static void PlaySound()
        {
            Task.Run(() =>
            {
                using var stream = Application.GetResourceStream(new Uri("/resources/sounds/sound.wav", UriKind.Relative))?.Stream;
                
                var notificationSound = new SoundPlayer(stream);
                notificationSound.PlaySync();
            });
        }

        public class ServerResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("output")]
            public string Output { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }
        }
    }
}
