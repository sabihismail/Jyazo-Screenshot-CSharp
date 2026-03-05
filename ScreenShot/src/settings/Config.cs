using System;
using System.IO;
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
                SaveConfig(Server);
            }
        }

        public void SaveConfig(string server)
        {
            Server = server;

            try
            {
                var configFile = Constants.CONFIG_FILE;
                Directory.CreateDirectory(Path.GetDirectoryName(configFile) ?? throw new InvalidOperationException("configFile null"));

                var configContainer = new ConfigContainer
                {
                    Server = !string.IsNullOrWhiteSpace(server) ? Encryption.SimpleEncryptWithPassword(server) : "",
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
                    Server = Server,
                    OAuth2Token = OAuth2Token
                };

                Server = !string.IsNullOrWhiteSpace(configContainer.Server) ? Encryption.SimpleDecryptWithPassword(configContainer.Server) : "";
                OAuth2Token = !string.IsNullOrWhiteSpace(configContainer.OAuth2Token) ? Encryption.SimpleDecryptWithPassword(configContainer.OAuth2Token) : "";
            }
            catch (Exception e)
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                Logging.Log("The config file is corrupted! All values have been reset.\nError:" + e.Message);
                SaveConfig(Server);
            }
        }

        public void SetOAuth2Token(string token)
        {
            OAuth2Token = token;
            SaveConfig(Server);
        }

        private class ConfigContainer
        {
            public string Server { get; set; }

            public string OAuth2Token { get; set; }
        }
    }
}
