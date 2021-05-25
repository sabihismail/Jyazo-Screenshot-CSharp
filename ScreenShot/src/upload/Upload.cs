using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using ScreenShot.src.tools;

namespace ScreenShot.src.upload
{
    public static class Upload
    {
        public static void UploadFile(string file, Settings settings, Config config)
        {
            var result = UploadToServer(file, config);

            if (string.IsNullOrWhiteSpace(result)) return;

            if (settings.EnableSound)
            {
                PlaySound();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Clipboard.SetText(result);
                Process.Start(result);
            });
        }

        private static void PlaySound()
        {
            Task.Run(() =>
            {
                using (var stream = Application.GetResourceStream(new Uri("/resources/sounds/sound.wav", UriKind.Relative))?.Stream)
                {
                    var notificationSound = new SoundPlayer(stream);
                    notificationSound.PlaySync();
                }
            });
        }

        private static string UploadToServer(string file, Config config)
        {
            using (var client = new UploadHttpClient(config))
            using (var formData = new MultipartFormDataContent())
            {
                /*var contentType = formData.Headers.ContentType.ToString();
                contentType = contentType.Replace("\"", "");
                formData.Headers.Remove("Content-Type");

                formData.Headers.TryAddWithoutValidation("Content-Type", contentType);*/

                var str = WindowInformation.ActiveWindow;

                if (string.IsNullOrWhiteSpace(str))
                {
                    str = "";
                }

                formData.Headers.Add("title", Convert.ToBase64String(Encoding.UTF8.GetBytes(str)));

                var streamContent = new StreamContent(File.OpenRead(file));
                streamContent.Headers.Add("Content-Type", FileUtils.GetContentType(Path.GetExtension(file)));
                formData.Add(streamContent, "uploaded_image", Path.GetFileName(file));

                var server = config.Server;
                if (!config.EnableOAuth2)
                {
                    if (!string.IsNullOrWhiteSpace(config.ServerPassword))
                    {
                        formData.Headers.Add("uploadpassword", config.ServerPassword);
                    }
                }
                else
                {
                    server = WebBrowserUtil.JoinURL(server, Constants.API_ENDPOINT_UPLOAD_SCREENSHOT);
                }

                using (var httpResponse = client.HttpClient.PostAsync(server, formData))
                {
                    var resultStr = "";
                    try
                    {
                        resultStr = httpResponse.Result.Content.ReadAsStringAsync().Result;
                    }
                    catch
                    {
                    }

                    var result = JsonConvert.DeserializeObject<ServerResponse>(resultStr);
                    if (result == null)
                    {
                        Logging.Log("No response from server.");
                        return "";
                    }

                    if (!result.Success)
                    {
                        Logging.Log("The server responded with:\n" + result.Error);
                        return "";
                    }

                    return result.Output;
                }
            }
        }

        public class UploadHttpClient : IDisposable
        {
            public readonly HttpClient HttpClient;
            private readonly HttpClientHandler Handler;

            public UploadHttpClient(Config config)
            {
                if (config.EnableOAuth2)
                {
                    var cookieContainer = new CookieContainer();
                    foreach (var cookie in config.OAuth2CookiesDotNet)
                    {
                        cookieContainer.Add(cookie);
                    }

                    Handler = new HttpClientHandler()
                    {
                        CookieContainer = cookieContainer
                    };

                    HttpClient = new HttpClient(Handler);
                }
                else
                {
                    HttpClient = new HttpClient();
                }
            }

            public void Dispose()
            {
                HttpClient.Dispose();
                Handler?.Dispose();
            }
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
