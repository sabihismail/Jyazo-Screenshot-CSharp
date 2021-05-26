using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ScreenShot.src.upload
{
    public class Util
    {
        public static string JoinURL(string path, string endpoint)
        {
            if (path.EndsWith("/") && endpoint.StartsWith("/"))
            {
                endpoint = endpoint.Substring(0, endpoint.Length - 1);
            }
            else if (!path.EndsWith("/") && !endpoint.StartsWith("/"))
            {
                endpoint = "/" + endpoint;
            }

            return path + endpoint;
        }

        public class CookieHttpClient : IDisposable
        {
            public readonly HttpClient HttpClient;
            private readonly HttpClientHandler Handler;

            public CookieHttpClient(Config config, bool allowAutoRedirect = true)
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
                        CookieContainer = cookieContainer,
                        AllowAutoRedirect = allowAutoRedirect
                    };

                    HttpClient = new HttpClient(Handler);
                }
                else
                {
                    HttpClient = new HttpClient();
                }
            }

            public Task<HttpResponseMessage> PostAsync(string server, MultipartFormDataContent formData)
            {
                return HttpClient.PostAsync(server, formData);
            }

            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
            {
                return HttpClient.SendAsync(request);
            }

            public void Dispose()
            {
                HttpClient.Dispose();
                Handler?.Dispose();
            }
        }
    }
}
