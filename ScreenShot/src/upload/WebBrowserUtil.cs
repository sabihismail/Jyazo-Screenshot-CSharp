using ScreenShot.src.tools;
using ScreenShot.views;
using System;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace ScreenShot.src.upload
{
    public class WebBrowserUtil
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

        public static void CheckIfOAuth2CredentialsValid(Config config, Action callback)
        {
            var fullURL = JoinURL(config.Server, Constants.API_ENDPOINT_IS_AUTHORIZED);

            var cookieContainer = new CookieContainer();
            foreach (var cookie in config.OAuth2CookiesDotNet)
            {
                cookieContainer.Add(cookie);
            }

            using (var handler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer,
                AllowAutoRedirect = false
            })
            using (var client = new HttpClient(handler))
            {
                var uri = new Uri(fullURL);
                var request = new HttpRequestMessage()
                {
                    RequestUri = uri,
                    Method = HttpMethod.Get,
                };

                HttpResponseMessage response;
                try
                {
                    response = client.SendAsync(request).Result;
                }
                catch
                {
                    Logging.Log("Could not connect to " + request + ". Exiting...");
                    Application.Current.Shutdown();
                    return;
                }

                var status = (int)response.StatusCode;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Application.Current.Dispatcher.Invoke(callback);
                    return;
                }

                if (status >= 300 && status <= 399)
                {
                    var redirectUri = response.Headers.Location;

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

                    browserWindow.Closed += (object sender, EventArgs e) =>
                    {
                        var host = uri.Host;

                        var cookies = browserWindow.CookiesDotNet
                            .FindAll(x => x.Domain.Contains(host));

                        config.SetOAuth2Cookies(cookies);

                        Application.Current.Dispatcher.Invoke(callback);
                    };

                    browserWindow.Show();
                }
            }
        }
    }
}
