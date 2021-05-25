using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScreenShot.src.tools;

namespace ScreenShot.src
{
    public class Config
    {
        public string Server = "";

        public string ServerPassword = "";

        public bool EnableGfycatUpload = true;

        public string GfycatClientID = "";

        public string GfycatClientSecret = "";

        public bool EnableOAuth2 = true;

        public List<CookieJSON> OAuth2Cookies = new List<CookieJSON>();

        public List<Cookie> OAuth2CookiesDotNet => CookieJSONToCookie(OAuth2Cookies);

        public Config()
        {
            var configFile = Constants.CONFIG_FILE;

            if (File.Exists(configFile))
            {
                UpdateConfig();
            }
            else
            {
                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, OAuth2Cookies);
            }
        }

        public void SaveConfig(string server, string serverPassword, bool enableGfycatUpload, string gfycatClientID, string gfycatClientSecret, bool enableOAuth2,
            List<CookieJSON> oAuth2Cookies = null)
        {
            Server = server;
            ServerPassword = serverPassword;
            EnableGfycatUpload = enableGfycatUpload;
            GfycatClientID = gfycatClientID;
            GfycatClientSecret = gfycatClientSecret;
            EnableOAuth2 = enableOAuth2;
            if (oAuth2Cookies != null)
            {
                OAuth2Cookies = oAuth2Cookies;
            }

            try
            {
                var configFile = Constants.CONFIG_FILE;
                Directory.CreateDirectory(Path.GetDirectoryName(configFile) ?? throw new InvalidOperationException("configFile null"));
                
                var configContainer = new ConfigContainer()
                {
                    Server = !string.IsNullOrWhiteSpace(server) ? Encryption.SimpleEncryptWithPassword(server) : "",
                    ServerPassword = !string.IsNullOrWhiteSpace(serverPassword) ? Encryption.SimpleEncryptWithPassword(serverPassword) : "",
                    EnableGfycatUpload = enableGfycatUpload,
                    GfycatClientID = !string.IsNullOrWhiteSpace(gfycatClientID) ? Encryption.SimpleEncryptWithPassword(gfycatClientID) : "",
                    GfycatClientSecret = !string.IsNullOrWhiteSpace(gfycatClientSecret) ? Encryption.SimpleEncryptWithPassword(gfycatClientSecret) : "",
                    EnableOAuth2 = enableOAuth2,
                    OAuth2Cookies = EncryptCookies(OAuth2Cookies),
                };

                var jsonStr = JsonConvert.SerializeObject(configContainer);

                File.WriteAllText(configFile, JToken.Parse(jsonStr).ToString());
            }
            catch (Exception e)
            {
                Logging.Log(e);
            }
        }

        private List<CookieJSON> EncryptCookies(List<CookieJSON> oAuth2Cookies)
        {
            return oAuth2Cookies.Select(x => new CookieJSON(
                Encryption.SimpleEncryptWithPassword(x.Name),
                Encryption.SimpleEncryptWithPassword(x.Value),
                Encryption.SimpleEncryptWithPassword(x.Path),
                Encryption.SimpleEncryptWithPassword(x.Domain))
            ).ToList();
        }

        private List<CookieJSON> DecryptCookies(List<CookieJSON> oAuth2Cookies)
        {
            return oAuth2Cookies.Select(x => new CookieJSON(
                Encryption.SimpleDecryptWithPassword(x.Name),
                Encryption.SimpleDecryptWithPassword(x.Value),
                Encryption.SimpleDecryptWithPassword(x.Path),
                Encryption.SimpleDecryptWithPassword(x.Domain))
            ).ToList();
        }

        private void UpdateConfig()
        {
            var configFile = Constants.CONFIG_FILE;

            try
            {
                var jsonStr = File.ReadAllText(configFile);

                var configContainer = JsonConvert.DeserializeObject<ConfigContainer>(jsonStr);

                Server = !string.IsNullOrWhiteSpace(configContainer.Server) ? Encryption.SimpleDecryptWithPassword(configContainer.Server) : "";
                ServerPassword = !string.IsNullOrWhiteSpace(configContainer.ServerPassword) ? Encryption.SimpleDecryptWithPassword(configContainer.ServerPassword) : "";
                EnableGfycatUpload = configContainer.EnableGfycatUpload;
                GfycatClientID = !string.IsNullOrWhiteSpace(configContainer.GfycatClientID) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientID) : "";
                GfycatClientSecret = !string.IsNullOrWhiteSpace(configContainer.GfycatClientSecret) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientSecret) : "";
                EnableOAuth2 = configContainer.EnableOAuth2;
                OAuth2Cookies = DecryptCookies(configContainer.OAuth2Cookies);
            }
            catch (Exception e)
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                Logging.Log("The config file is corrupted! All values have been reset.");
                Logging.Log(e);

                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, OAuth2Cookies);
            }
        }

        public void SetOAuth2Cookies(List<Cookie> cookiesDotNet)
        {
            var cookies = cookiesDotNet.Select(x => new CookieJSON(x.Name, x.Value, x.Path, x.Domain))
                .ToList();

            SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2, cookies);
        }

        /*
        private List<CookieJSON> CookieToCookieJSON(List<Cookie> cookies)
        {
            return cookies.Select(x => new CookieJSON(x.Name, x.Value, x.Path, x.Domain))
                .ToList();
        }
        */

        private List<Cookie> CookieJSONToCookie(List<CookieJSON> cookies)
        {
            return cookies.Select(x => new Cookie(x.Name, x.Value, x.Path, x.Domain))
                .ToList();
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
            public string Name { get; set; }

            public string Value { get; set; }

            public string Path { get; set; }

            public string Domain { get; set; }

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
