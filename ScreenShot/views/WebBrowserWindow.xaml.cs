using CefSharp;
using ScreenShot.src.upload;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ScreenShot.views
{
    public partial class WebBrowserWindow : Window
    {
        private readonly CEFBrowser cefBrowser;

        public List<Cookie> Cookies => cefBrowser.Cookies;

        public List<System.Net.Cookie> CookiesDotNet => Cookies.Select(x => new System.Net.Cookie(x.Name, x.Value, x.Path, x.Domain))
            .ToList();

        public WebBrowserWindow(string url, string authorizedURL)
        {
            InitializeComponent();

            cefBrowser = new CEFBrowser(url, authorizedURL, Close);

            GrdMain.Children.Add(cefBrowser.Browser);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            cefBrowser.Close();

            base.OnClosing(e);
        }
    }
}
