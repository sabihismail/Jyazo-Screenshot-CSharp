using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ScreenShot.src.tools;

namespace ScreenShot.src.upload
{
    [Obsolete("Not used any more since gif capture is more efficient now", true)]
    public class GfycatUpload
    {
        private const string API_ENDPOINT = "https://api.gfycat.com/v1/";
        private const string API_ENDPOINT_POST_KEY = API_ENDPOINT + "gfycats";
        private const string API_ENDPOINT_GET_STATUS = API_ENDPOINT + "gfycats/fetch/status/";

        private const string URL_START = "https://gfycat.com/";

        private readonly Config config;

        public string URL;

        public GfycatUpload(Config config, string file)
        {
            this.config = config;

            var oAuthKey = GenerateOAuthKey().Result;
            var uploadInformation = RetrieveUploadInformation(oAuthKey);

            URL = URL_START + Upload(uploadInformation, file);
        }

        private string Upload(object uploadInformation, string file)
        {
            throw new NotImplementedException();
        }

        private string RetrieveUploadInformation(string oAuthKey)
        {
            throw new NotImplementedException();
        }

        private async Task<string> GenerateOAuthKey()
        {
            try
            {
                var data = new Dictionary<string, string>()
                {
                    {"client_id", config.GfycatClientID},
                    {"client_secret", config.GfycatClientSecret},
                    {"grant_type", "client_credentials"},
                };

                using (var client = new HttpClient())
                using (var content = new FormUrlEncodedContent(data))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    using (var response = await client.PostAsync("https://api.gfycat.com/v1/oauth/token", content))
                    {
                        var jsonStr = await response.Content.ReadAsStringAsync();
                        var json = JsonConvert.DeserializeObject<OAuthResponse>(jsonStr);

                        if (!string.IsNullOrWhiteSpace(json.AccessToken))
                        {
                            return json.AccessToken;
                        }

                        Logging.Log($"Gfycat Failed Acquiring Access Token: Error Code: {json.ErrorMessage.Code}\nError Message: \"{json.ErrorMessage.Description}\"");
                        return null;
                    }
                }
            }
            catch (IOException e)
            {
                Logging.Log(e);
            }

            return "";
        }

        public class OAuthResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("errorMessage")]
            public OAuthResponseError ErrorMessage { get; set; }

            public class OAuthResponseError
            {
                [JsonProperty("code")]
                public string Code { get; set; }

                [JsonProperty("description")]
                public string Description { get; set; }
            }
        }
    }
}
