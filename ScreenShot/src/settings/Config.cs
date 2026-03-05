using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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

        public string OAuth2Token = "";

        public Config()
        {
            var configFile = Constants.CONFIG_FILE;

            if (File.Exists(configFile))
            {
                UpdateConfig();
            }
            else
            {
                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2);
            }
        }

        public void SaveConfig(string server, string serverPassword, bool enableGfycatUpload, string gfycatClientID, string gfycatClientSecret, bool enableOAuth2)
        {
            Server = server;
            ServerPassword = serverPassword;
            EnableGfycatUpload = enableGfycatUpload;
            GfycatClientID = gfycatClientID;
            GfycatClientSecret = gfycatClientSecret;
            EnableOAuth2 = enableOAuth2;

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
                    OAuth2Token = !string.IsNullOrWhiteSpace(OAuth2Token) ? Encryption.SimpleEncryptWithPassword(OAuth2Token) : ""
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
                    OAuth2Token = OAuth2Token,
                    Server = Server,
                    ServerPassword = ServerPassword
                };

                Server = !string.IsNullOrWhiteSpace(configContainer.Server) ? Encryption.SimpleDecryptWithPassword(configContainer.Server) : "";
                ServerPassword = !string.IsNullOrWhiteSpace(configContainer.ServerPassword) ? Encryption.SimpleDecryptWithPassword(configContainer.ServerPassword) : "";
                EnableGfycatUpload = configContainer.EnableGfycatUpload;
                GfycatClientID = !string.IsNullOrWhiteSpace(configContainer.GfycatClientID) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientID) : "";
                GfycatClientSecret = !string.IsNullOrWhiteSpace(configContainer.GfycatClientSecret) ? Encryption.SimpleDecryptWithPassword(configContainer.GfycatClientSecret) : "";
                EnableOAuth2 = configContainer.EnableOAuth2;
                OAuth2Token = !string.IsNullOrWhiteSpace(configContainer.OAuth2Token) ? Encryption.SimpleDecryptWithPassword(configContainer.OAuth2Token) : "";
            }
            catch (Exception e)
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                Logging.Log("The config file is corrupted! All values have been reset.\nError:" + e.Message);

                SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2);
            }
        }

        public void SetOAuth2Token(string token)
        {
            OAuth2Token = token;
            SaveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret, EnableOAuth2);
        }

        private class ConfigContainer
        {
            public string Server { get; set; }

            public string ServerPassword { get; set; }

            public bool EnableGfycatUpload { get; set; }

            public string GfycatClientID { get; set; }

            public string GfycatClientSecret { get; set; }

            public bool EnableOAuth2 { get; set; }

            public string OAuth2Token { get; set; }
        }
    }
}
