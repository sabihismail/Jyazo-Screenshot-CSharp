using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ScreenShot.src.settings;

namespace ScreenShot.src.tools.util
{
    public static class URLUtils
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
            private readonly HttpClient httpClient;
            private readonly HttpClientHandler handler;

            public CookieHttpClient(Config config, bool allowAutoRedirect = true)
            {
                if (config.EnableOAuth2)
                {
                    var cookieContainer = new CookieContainer();
                    foreach (var cookie in config.OAuth2CookiesDotNet)
                    {
                        cookieContainer.Add(cookie);
                    }

                    handler = new HttpClientHandler
                    {
                        CookieContainer = cookieContainer,
                        AllowAutoRedirect = allowAutoRedirect
                    };

                    httpClient = new HttpClient(handler);
                }
                else
                {
                    httpClient = new HttpClient();
                }
            }

            public Task<HttpResponseMessage> PostAsync(string server, MultipartFormDataContent formData)
            {
                return httpClient.PostAsync(server, formData);
            }

            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
            {
                return httpClient.SendAsync(request);
            }

            public void Dispose()
            {
                httpClient.Dispose();
                handler?.Dispose();
            }
        }
    }
}
