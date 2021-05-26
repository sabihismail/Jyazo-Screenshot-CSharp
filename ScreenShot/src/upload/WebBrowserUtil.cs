using ScreenShot.src.tools;
using ScreenShot.views;
using System;
using System.Net;
using System.Net.Http;
using System.Windows;
using static ScreenShot.src.upload.Util;

namespace ScreenShot.src.upload
{
    public class WebBrowserUtil
    {

        public static void CheckIfOAuth2CredentialsValid(Config config, Action callback)
        {
            var fullURL = JoinURL(config.Server, Constants.API_ENDPOINT_IS_AUTHORIZED);

            using (var client = new CookieHttpClient(config, false))
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

                    response.Dispose();

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
