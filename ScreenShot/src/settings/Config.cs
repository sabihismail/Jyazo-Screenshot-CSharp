using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScreenShot.src.tools;
using ScreenShot.views;

namespace ScreenShot.src.settings
{
    public class Config
    {
        private string serverImpl = "";
        
        public string Server
        {
            get => App.isDevMode == 1 ? Constants.OVERRIDE_SERVER : serverImpl;
            private set => serverImpl = value;
        }

        public string ServerPassword = "";

        public bool EnableGfycatUpload;

        public string GfycatClientID = "";

        public string GfycatClientSecret = "";

        public bool EnableOAuth2 = true;

        public IEnumerable<Cookie> OAuth2CookiesDotNet => CookieJSONToCookie(oAuth2Cookies);

        private List<CookieJSON> oAuth2Cookies = new();

        public Config()
        {
            var configFile = Constants.CONFIG_FILE;

            if (File.Exists(configFile))
            {
                UpdateConfig();
            }
            else
            {
                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, oAuth2Cookies);
            }
        }

        public void SaveConfig(string server, string serverPassword, bool enableGfycatUpload, string gfycatClientID, string gfycatClientSecret, bool enableOAuth2,
            List<CookieJSON> oAuth2CookiesIn = null)
        {
            Server = server;
            ServerPassword = serverPassword;
            EnableGfycatUpload = enableGfycatUpload;
            GfycatClientID = gfycatClientID;
            GfycatClientSecret = gfycatClientSecret;
            EnableOAuth2 = enableOAuth2;
            if (oAuth2CookiesIn != null)
            {
                oAuth2Cookies = oAuth2CookiesIn;
            }

            oAuth2Cookies ??= new List<CookieJSON>();

            try
            {
                var configFile = Constants.CONFIG_FILE;
                Directory.CreateDirectory(Path.GetDirectoryName(configFile) ?? throw new InvalidOperationException("configFile null"));
                
                var configContainer = new ConfigContainer
                {
                    Server = !string.IsNullOrWhiteSpace(server) ? Encryption.SimpleEncryptWithPassword(server) : "",
                    ServerPassword = !string.IsNullOrWhiteSpace(serverPassword) ? Encryption.SimpleEncryptWithPassword(serverPassword) : "",
                    EnableGfycatUpload = enableGfycatUpload,
                    GfycatClientID = !string.IsNullOrWhiteSpace(gfycatClientID) ? Encryption.SimpleEncryptWithPassword(gfycatClientID) : "",
                    GfycatClientSecret = !string.IsNullOrWhiteSpace(gfycatClientSecret) ? Encryption.SimpleEncryptWithPassword(gfycatClientSecret) : "",
                    EnableOAuth2 = enableOAuth2,
                    OAuth2Cookies = EncryptCookies(oAuth2Cookies)
                };

                var jsonStr = JsonConvert.SerializeObject(configContainer);

                File.WriteAllText(configFile, JToken.Parse(jsonStr).ToString());
            }
            catch (Exception e)
            {
                Logging.Log(e);
            }
        }

        private void UpdateConfig()
        {
            var configFile = Constants.CONFIG_FILE;

            try
            {
                var jsonStr = File.ReadAllText(configFile);

                var configContainer = JsonConvert.DeserializeObject<ConfigContainer>(jsonStr) ?? new ConfigContainer
                {
                    EnableGfycatUpload = EnableGfycatUpload,
                    EnableOAuth2 = EnableOAuth2,
                    GfycatClientID = GfycatClientID,
                    GfycatClientSecret = GfycatClientSecret,
                    OAuth2Cookies = oAuth2Cookies,
                    Server = Server,
                    ServerPassword = ServerPassword
                };

                Server = !string.IsNullOrWhiteSpace(configContainer.Server) ? Encryption.SimpleDecryptWithPassword(configContainer.Server) : "";
                ServerPassword = !string.IsNullOrWhiteSpace(configContainer.ServerPassword) ? Encryption.SimpleDecryptWithPassword(configContainer.ServerPassword) : "";
                EnableGfycatUpload = configContainer.EnableGfycatUpload;
                GfycatClientID = !string.IsNullOrWhiteSpace(configContainer.GfycatClientID) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientID) : "";
                GfycatClientSecret = !string.IsNullOrWhiteSpace(configContainer.GfycatClientSecret) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientSecret) : "";
                EnableOAuth2 = configContainer.EnableOAuth2;
                oAuth2Cookies = DecryptCookies(configContainer.OAuth2Cookies);
            }
            catch (Exception e)
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                Logging.Log("The config file is corrupted! All values have been reset.\nError:" + e.Message);

                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, oAuth2Cookies);
            }
        }

        public void SetOAuth2Cookies(IEnumerable<Cookie> cookiesDotNet)
        {
            var cookies = cookiesDotNet.Select(x => new CookieJSON(x.Name, x.Value, x.Path, x.Domain))
                .ToList();

            SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, cookies);
        }

        private static IEnumerable<Cookie> CookieJSONToCookie(IEnumerable<CookieJSON> cookies)
        {
            return cookies.Select(x => new Cookie(x.Name, x.Value, x.Path, x.Domain))
                .ToList();
        }

        // ReSharper disable once UnusedMember.Local
        private List<CookieJSON> CookieToCookieJSON(IEnumerable<Cookie> cookies)
        {
            return cookies.Select(x => new CookieJSON(x.Name, x.Value, x.Path, x.Domain))
                .ToList();
        }

        private static List<CookieJSON> EncryptCookies(IEnumerable<CookieJSON> oAuth2Cookies)
        {
            return oAuth2Cookies.Select(x => new CookieJSON(
                Encryption.SimpleEncryptWithPassword(x.Name),
                Encryption.SimpleEncryptWithPassword(x.Value),
                Encryption.SimpleEncryptWithPassword(x.Path),
                Encryption.SimpleEncryptWithPassword(x.Domain))
            ).ToList();
        }

        private static List<CookieJSON> DecryptCookies(IEnumerable<CookieJSON> oAuth2Cookies)
        {
            return oAuth2Cookies.Select(x => new CookieJSON(
                Encryption.SimpleDecryptWithPassword(x.Name),
                Encryption.SimpleDecryptWithPassword(x.Value),
                Encryption.SimpleDecryptWithPassword(x.Path),
                Encryption.SimpleDecryptWithPassword(x.Domain))
            ).ToList();
        }

        private class ConfigContainer
        {
            public string Server { get; set; }

            public string ServerPassword { get; set; }

            public bool EnableGfycatUpload { get; set; }

            public string GfycatClientID { get; set; }

            public string GfycatClientSecret { get; set; }

            public bool EnableOAuth2 { get; set; }

            public List<CookieJSON> OAuth2Cookies { get; set; }
        }

        public class CookieJSON
        {
            public string Name { get; }

            public string Value { get; }

            public string Path { get; }

            public string Domain { get; }

            public CookieJSON(string name, string value, string path, string domain)
            {
                Name = name;
                Value = value;
                Path = path;
                Domain = domain;
            }
        }
    }
}
