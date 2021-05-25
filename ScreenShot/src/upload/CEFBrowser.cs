using CefSharp;
using CefSharp.Wpf;
using ScreenShot.src.tools;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

namespace ScreenShot.src.upload
{
    public class CEFBrowser
    {
        private readonly LifeSpanHandler LifeSpanHandler = new LifeSpanHandler();

        public readonly ChromiumWebBrowser Browser;

        public List<Cookie> Cookies => LifeSpanHandler.CookieVisitor.Cookies;

        public CEFBrowser(string url, string authorizedURL, Action callback)
        {
            authorizedURL = authorizedURL.Trim();

            Browser = new ChromiumWebBrowser(url)
            {
                BrowserSettings =
                {
                    DefaultEncoding = "UTF-8",
                    WebGl = CefState.Disabled,
                },
                LifeSpanHandler = LifeSpanHandler
            };

            Browser.AddressChanged += (object sender, DependencyPropertyChangedEventArgs e) =>
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
            LifeSpanHandler.Close(Browser);
        }
    }

    public class LifeSpanHandler : ILifeSpanHandler
    {
        public readonly CookieVisitor CookieVisitor = new CookieVisitor();

        public bool IsClosed = false;

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

            IsClosed = true;
        }
    }

    public class CookieVisitor : ICookieVisitor
    {
        public readonly List<Cookie> Cookies = new List<Cookie>();
        private readonly ManualResetEvent GotAllCookies = new ManualResetEvent(false);

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            Cookies.Add(cookie);

            if (count == total - 1)
            {
                GotAllCookies.Set();
            }

            return true;
        }

        public void WaitForAllCookies()
        {
            GotAllCookies.WaitOne();
        }

        public void Dispose()
        {
            GotAllCookies.Dispose();
        }
    }
}
