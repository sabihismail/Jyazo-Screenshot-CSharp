using System;
using System.Collections.Generic;
using System.Threading;
using CefSharp;
using CefSharp.Wpf;

namespace ScreenShot.src.tools
{
    [Obsolete("Chromium Embedded Framework elements are no longer considered 'safe' and so many OAuth implementations no longer allow for them to be used, probably due to bad actors abusing it.")]
    public class CEFBrowser
    {
        private readonly LifeSpanHandler lifeSpanHandler = new();

        public readonly ChromiumWebBrowser Browser;

        public IEnumerable<Cookie> Cookies => lifeSpanHandler.CookieVisitor.Cookies;

        public CEFBrowser(string url, string authorizedURL, Action callback)
        {
            authorizedURL = authorizedURL.Trim();

            Browser = new ChromiumWebBrowser(url)
            {
                BrowserSettings =
                {
                    DefaultEncoding = "UTF-8",
                    WebGl = CefState.Disabled
                },
                LifeSpanHandler = lifeSpanHandler
            };

            Browser.AddressChanged += (_, e) =>
            {
                if (e.NewValue == null)
                {
                    return;
                }

                var address = Convert.ToString(e.NewValue).Trim();
                if (address == authorizedURL)
                {
                    callback();
                }
            };
        }

        public void Close()
        {
            lifeSpanHandler.Close(Browser);
        }
    }

    public class LifeSpanHandler : ILifeSpanHandler
    {
        public readonly CookieVisitor CookieVisitor = new();

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return false;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Close(chromiumWebBrowser);
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            return true;
        }

        public void Close(IWebBrowser chromiumWebBrowser)
        {
            var cookieManager = chromiumWebBrowser.GetCookieManager();

            if (cookieManager.VisitAllCookies(CookieVisitor))
            {
                CookieVisitor.WaitForAllCookies();
            }
        }
    }

    public class CookieVisitor : ICookieVisitor
    {
        public readonly List<Cookie> Cookies = new();
        private readonly ManualResetEvent gotAllCookies = new(false);

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            Cookies.Add(cookie);

            if (count == total - 1)
            {
                gotAllCookies.Set();
            }

            return true;
        }

        public void WaitForAllCookies()
        {
            gotAllCookies.WaitOne();
        }

        public void Dispose()
        {
            gotAllCookies.Dispose();
        }
    }
}
