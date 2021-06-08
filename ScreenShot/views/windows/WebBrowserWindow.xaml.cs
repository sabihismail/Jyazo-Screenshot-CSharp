using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CefSharp;
using ScreenShot.src.tools;

namespace ScreenShot.views.windows
{
    public partial class WebBrowserWindow
    {
        private readonly CEFBrowser cefBrowser;

        private IEnumerable<Cookie> Cookies => cefBrowser.Cookies;

        public List<System.Net.Cookie> CookiesDotNet => Cookies.Select(x => new System.Net.Cookie(x.Name, x.Value, x.Path, x.Domain))
            .ToList();

        public WebBrowserWindow(string url, string authorizedURL)
        {
            InitializeComponent();

            cefBrowser = new CEFBrowser(url, authorizedURL, Close);

            GrdMain.Children.Add(cefBrowser.Browser);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            cefBrowser.Close();
        }
    }
}
