using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ScreenShot.src.tools;
using ScreenShot.views;

namespace ScreenShot.src.settings
{
    public class Config
    {
        private string serverImpl = "";
        private string tokenImpl = "";
        private long tokenExpiresAtImpl = 0;

        public string Server
        {
            get => App.isDevMode == 1 ? Constants.OVERRIDE_SERVER : serverImpl;
            private set => serverImpl = value;
        }

        public string OAuth2Token
        {
            get => tokenImpl;
            set
            {
                tokenImpl = value;
                SaveToken(value);
            }
        }

        public long TokenExpiresAt
        {
            get => tokenExpiresAtImpl;
            set
            {
                tokenExpiresAtImpl = value;
                SaveTokenExpiresAt(value);
            }
        }

        public bool IsTokenExpired()
        {
            if (tokenExpiresAtImpl <= 0) return true; // No expiry info, consider expired
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return nowUnix >= tokenExpiresAtImpl;
        }

        private static readonly string DbPath = GetDatabasePath();

        private static string GetDatabasePath()
        {
            var configDir = Path.GetDirectoryName(Constants.CONFIG_FILE);
            if (string.IsNullOrEmpty(configDir))
            {
                // Fallback to AppData if config dir is invalid
                configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ArkaPrime", "Jyazo");
            }
            return Path.Combine(configDir, "config.db");
        }

        private static readonly string DbPassword = GetDatabasePassword();

        private static string GetDatabasePassword()
        {
            try
            {
                // Use DPAPI to derive password from Windows user credentials
                var configDir = Path.GetDirectoryName(GetDatabasePath());
                var keyFile = Path.Combine(configDir, ".dbkey");

                byte[] encryptedKey;

                if (File.Exists(keyFile))
                {
                    // Load existing encrypted key
                    encryptedKey = File.ReadAllBytes(keyFile);
                    Debug.WriteLine($"[CONFIG] Loaded existing DPAPI key");
                }
                else
                {
                    // Generate new random key and encrypt with DPAPI
                    var randomKey = new byte[32]; // 256-bit key
                    using (var rng = new RNGCryptoServiceProvider())
                    {
                        rng.GetBytes(randomKey);
                    }

                    encryptedKey = ProtectedData.Protect(randomKey, null, DataProtectionScope.CurrentUser);

                    // Save encrypted key for future runs
                    if (!string.IsNullOrEmpty(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                        File.WriteAllBytes(keyFile, encryptedKey);
                        File.SetAttributes(keyFile, FileAttributes.Hidden);
                        Debug.WriteLine($"[CONFIG] Generated and saved new DPAPI key");
                    }
                }

                // Decrypt key using DPAPI (only works for current Windows user)
                var decryptedKey = ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                var password = Convert.ToBase64String(decryptedKey);

                Debug.WriteLine($"[CONFIG] DPAPI password derived from Windows user credentials");
                return password;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONFIG] DPAPI password generation failed: {ex.Message}");
                // Fallback to environment variable or hardcoded value
                return System.Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "JyazoScreenShot2024";
            }
        }

        private SQLiteConnection connection;

        public Config()
        {
            Debug.WriteLine($"[CONFIG] Config constructor called");
            Debug.WriteLine($"[CONFIG] DbPath will be: {DbPath}");
            InitializeDatabase();
            if (connection != null)
            {
                Debug.WriteLine($"[CONFIG] Connection successful, loading config");
                LoadConfig();
            }
            else
            {
                Debug.WriteLine($"[CONFIG] ✗ Connection is null after initialization");
            }
        }

        private void InitializeDatabase()
        {
            try
            {
                var dir = Path.GetDirectoryName(DbPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Create database file if it doesn't exist
                if (!File.Exists(DbPath))
                {
                    SQLiteConnection.CreateFile(DbPath);
                }

                connection = new SQLiteConnection($"Data Source={DbPath};Version=3;Password={DbPassword};PRAGMA journal_mode = WAL;PRAGMA synchronous = NORMAL;");
                connection.Open();

                using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS config (
                        id INTEGER PRIMARY KEY,
                        server TEXT,
                        oauth2_token TEXT,
                        token_expires_at INTEGER
                    );
                    CREATE TABLE IF NOT EXISTS server_tokens (
                        base_url TEXT PRIMARY KEY,
                        oauth2_token TEXT,
                        token_expires_at INTEGER,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                ";
                createCommand.ExecuteNonQuery();

                Debug.WriteLine($"[CONFIG] ✓ Database initialized successfully");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[CONFIG] ✗ Database initialization failed: {e.Message}");
                Logging.Log($"Database initialization error: {e.Message}");
                connection?.Dispose();
                connection = null;
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (connection == null)
                {
                    Logging.Log("Config load error: Database not initialized");
                    return;
                }

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT server, oauth2_token, token_expires_at FROM config LIMIT 1";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Server = reader["server"]?.ToString() ?? "";
                    tokenImpl = reader["oauth2_token"]?.ToString() ?? "";

                    if (reader["token_expires_at"] != DBNull.Value &&
                        long.TryParse(reader["token_expires_at"].ToString(), out var expiresAt))
                    {
                        tokenExpiresAtImpl = expiresAt;
                    }
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
                if (connection == null)
                {
                    Logging.Log("Config save error: Database not initialized");
                    return;
                }

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

        private void SaveToken(string token)
        {
            try
            {
                if (connection == null)
                {
                    Logging.Log("Config save error: Database not initialized");
                    return;
                }

                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE config SET oauth2_token = @token WHERE id = 1";
                command.Parameters.AddWithValue("@token", token ?? "");
                command.ExecuteNonQuery();

                Debug.WriteLine($"[CONFIG] ✓ Token saved");
            }
            catch (Exception e)
            {
                Logging.Log($"Config save error: {e.Message}");
            }
        }

        private void SaveTokenExpiresAt(long expiresAt)
        {
            try
            {
                if (connection == null)
                {
                    Logging.Log("Config save error: Database not initialized");
                    return;
                }

                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE config SET token_expires_at = @expiresAt WHERE id = 1";
                command.Parameters.AddWithValue("@expiresAt", expiresAt);
                command.ExecuteNonQuery();

                Debug.WriteLine($"[CONFIG] ✓ Token expiry saved: {expiresAt}");
            }
            catch (Exception e)
            {
                Logging.Log($"Config save error: {e.Message}");
            }
        }
    }
}
