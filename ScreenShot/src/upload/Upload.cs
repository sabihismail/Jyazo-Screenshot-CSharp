using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
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

            /*
            if (config.EnableGfycatUpload && Path.GetExtension(file) == ".gif")
            {
                var gfycatUpload = new GfycatUpload(config, file);

                result = gfycatUpload.URL;
            }
            else
            {
                result = UploadToServer(file, config);
            }
            */

            if (string.IsNullOrWhiteSpace(result)) return;

            if (settings.enableSound)
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
            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                formData.Headers.Add("title", WindowInformation.ActiveWindow);
                
                var streamContent = new StreamContent(File.OpenRead(file));
                streamContent.Headers.Add("Content-Type", FileUtils.GetContentType(Path.GetExtension(file)));
                formData.Add(streamContent, "uploaded_image", Path.GetFileName(file));

                if (!string.IsNullOrWhiteSpace(config.ServerPassword))
                {
                    formData.Headers.Add("uploadpassword", config.ServerPassword);
                }

                var httpResponse = client.PostAsync(config.Server, formData).Result;
                var resultStr = httpResponse.Content.ReadAsStringAsync().Result;

                var result = JsonConvert.DeserializeObject<ServerResponse>(resultStr);

                if (result.Success)
                {
                    return result.Output;
                }

                Logging.Log("The server responded with:\n" + result.Error);

                return "";
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
