using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
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
            if (string.IsNullOrWhiteSpace(config.Server) || !config.EnableOAuth2 && string.IsNullOrWhiteSpace(config.ServerPassword))
            {
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
                Clipboard.SetText(result);

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
            using var client = new CookieHttpClient(config);
            using var formData = new MultipartFormDataContent();
            
            var streamContent = new StreamContent(File.OpenRead(file));
            streamContent.Headers.Add("Content-Type", FileUtils.GetContentType(Path.GetExtension(file)));
            formData.Add(streamContent, "uploaded_image", Path.GetFileName(file));

            var titleContent = new StringContent(!string.IsNullOrWhiteSpace(WindowInformation.ActiveWindow) ? WindowInformation.ActiveWindow : "", Encoding.UTF8);
            formData.Add(titleContent, "title");

            var server = config.Server;
            if (!config.EnableOAuth2)
            {
                if (!string.IsNullOrWhiteSpace(config.ServerPassword))
                {
                    formData.Headers.Add("upload_password", config.ServerPassword);
                }
            }
            else
            {
                server = JoinURL(server, Constants.API_ENDPOINT_UPLOAD_SCREENSHOT);
            }

            try
            {
                using var httpResponse = await client.PostAsync(server, formData);
                    
                var resultStr = await httpResponse.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<ServerResponse>(resultStr);
                if (result == null)
                {
                    Logging.Log("No response from server.");
                    return "";
                }

                if (result.Success) return result.Output;
                    
                Logging.Log("The server responded with:\n" + result.Error);
                return "";
            }
            catch (Exception e)
            {
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
