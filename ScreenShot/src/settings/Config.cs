using System;
using System.Data.SQLite;
using System.IO;
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

        private static readonly string DbPath = Path.Combine(
            Path.GetDirectoryName(Constants.CONFIG_FILE) ?? throw new InvalidOperationException("config path null"),
            "config.db");

        private static readonly string DbPassword = "JyazoScreenShot2024";

        public Config()
        {
            InitializeDatabase();
            LoadConfig();
        }

        private void InitializeDatabase()
        {
            try
            {
                if (!File.Exists(DbPath))
                {
                    SQLiteConnection.CreateFile(DbPath);
                }

                using var connection = new SQLiteConnection($"Data Source={DbPath};Password={DbPassword};");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS config (
                        id INTEGER PRIMARY KEY,
                        server TEXT,
                        oauth2_token TEXT
                    )
                ";
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logging.Log($"Database initialization error: {e.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={DbPath};Password={DbPassword};");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT server, oauth2_token FROM config LIMIT 1";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Server = reader["server"]?.ToString() ?? "";
                    OAuth2Token = reader["oauth2_token"]?.ToString() ?? "";
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Config load error: {e.Message}");
            }
        }

        public void SaveConfig(string server)
        {
            Server = server;

            try
            {
                using var connection = new SQLiteConnection($"Data Source={DbPath};Password={DbPassword};");
                connection.Open();

                // Check if record exists
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM config";
                var count = (long)checkCommand.ExecuteScalar();

                using var command = connection.CreateCommand();
                if (count > 0)
                {
                    command.CommandText = "UPDATE config SET server = @server WHERE id = 1";
                }
                else
                {
                    command.CommandText = "INSERT INTO config (id, server) VALUES (1, @server)";
                }

                command.Parameters.AddWithValue("@server", server ?? "");
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logging.Log($"Config save error: {e.Message}");
            }
        }

        public void SetOAuth2Token(string token)
        {
            OAuth2Token = token;

            try
            {
                using var connection = new SQLiteConnection($"Data Source={DbPath};Password={DbPassword};");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE config SET oauth2_token = @token WHERE id = 1";
                command.Parameters.AddWithValue("@token", token ?? "");
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logging.Log($"Token save error: {e.Message}");
            }
        }
    }
}
