using System;
using System.IO;
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
        
        public Config()
        {
            var configFile = Constants.CONFIG_FILE;

            if (File.Exists(configFile))
            {
                updateConfig();
            }
            else
            {
                saveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret);
            }
        }

        public void saveConfig(string server, string serverPassword, bool enableGfycatUpload, string gfycatClientID, string gfycatClientSecret)
        {
            Server = server;
            ServerPassword = serverPassword;
            EnableGfycatUpload = enableGfycatUpload;
            GfycatClientID = gfycatClientID;
            GfycatClientSecret = gfycatClientSecret;

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
                    GfycatClientSecret = !string.IsNullOrWhiteSpace(gfycatClientSecret) ? Encryption.SimpleEncryptWithPassword(gfycatClientSecret) : ""
                };

                var jsonStr = JsonConvert.SerializeObject(configContainer);

                File.WriteAllText(configFile, JToken.Parse(jsonStr).ToString());
            }
            catch (Exception e)
            {
                Logging.Log(e);
            }
        }

        private void updateConfig()
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
            }
            catch (Exception e)
            {
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }

                Logging.Log("The config file is corrupted! All values have been reset.");

                saveConfig(Server, ServerPassword, EnableGfycatUpload, GfycatClientID, GfycatClientSecret);
            }
        }

        private class ConfigContainer
        {
            public string Server { get; set; }
            public string ServerPassword { get; set; }
            public bool EnableGfycatUpload { get; set; }
            public string GfycatClientID { get; set; }
            public string GfycatClientSecret { get; set; }
        }
    }
}
