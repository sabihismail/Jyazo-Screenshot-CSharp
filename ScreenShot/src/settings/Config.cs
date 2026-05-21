using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using ScreenShot.src.tools;
using ScreenShot.views;

namespace ScreenShot.src.settings
{
    public class Config : IDisposable
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
            get => GetTokenForServer();
            set => SaveTokenAndExpiry(value, tokenExpiresAtImpl);
        }

        public long TokenExpiresAt
        {
            get => tokenExpiresAtImpl;
            set => SaveTokenAndExpiry(tokenImpl, value);
        }

        public bool IsTokenExpired()
        {
            if (string.IsNullOrWhiteSpace(tokenImpl)) return true;
            var exp = GetJwtExpiry(tokenImpl) ?? tokenExpiresAtImpl;
            if (exp <= 0) return true;
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp;
        }

        private static long? GetJwtExpiry(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3) return null;

                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var obj = JObject.Parse(json);
                return obj["exp"]?.Value<long>();
            }
            catch { return null; }
        }

        private static readonly string DbPath = GetDatabasePath();

        private static string GetDatabasePath()
        {
            // Use user profile: %USERPROFILE%\ArkaPrime\Jyazo\settings.db
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(userProfile, "ArkaPrime", "Jyazo");
            return Path.Combine(configDir, "settings.db");
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

        public void Dispose()
        {
            connection?.Dispose();
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

                connection = new SQLiteConnection($"Data Source={DbPath};Version=3;Password={DbPassword};");
                connection.Open();

                // Configure database pragmas for performance
                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                pragmaCmd.ExecuteNonQuery();

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

                // Schema migration: add missing columns if they don't exist
                try
                {
                    // Check and add token_expires_at to server_tokens table
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = "PRAGMA table_info(server_tokens)";
                    using var reader = checkCommand.ExecuteReader();
                    var columns = new HashSet<string>();
                    while (reader.Read())
                    {
                        columns.Add(reader["name"].ToString());
                    }

                    // Add token_expires_at column if missing
                    if (!columns.Contains("token_expires_at"))
                    {
                        using var alterCommand = connection.CreateCommand();
                        alterCommand.CommandText = "ALTER TABLE server_tokens ADD COLUMN token_expires_at INTEGER DEFAULT 0";
                        alterCommand.ExecuteNonQuery();
                        Debug.WriteLine($"[CONFIG] Added missing token_expires_at column to server_tokens");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CONFIG] Schema migration warning: {ex.Message}");
                }

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

                    // Load token for the configured server
                    if (!string.IsNullOrWhiteSpace(serverImpl))
                    {
                        var baseUrl = ExtractBaseUrl(serverImpl);
                        using var tokenCommand = connection.CreateCommand();
                        tokenCommand.CommandText = "SELECT oauth2_token, token_expires_at FROM server_tokens WHERE base_url = @base_url LIMIT 1";
                        tokenCommand.Parameters.AddWithValue("@base_url", baseUrl);

                        using var tokenReader = tokenCommand.ExecuteReader();
                        if (tokenReader.Read())
                        {
                            tokenImpl = tokenReader["oauth2_token"]?.ToString() ?? "";

                            if (tokenReader["token_expires_at"] != DBNull.Value &&
                                long.TryParse(tokenReader["token_expires_at"].ToString(), out var expiresAt))
                            {
                                tokenExpiresAtImpl = expiresAt;
                            }

                            if (!string.IsNullOrWhiteSpace(tokenImpl))
                            {
                                Debug.WriteLine($"[CONFIG] Loaded existing token for server: {baseUrl}");
                            }
                        }
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
                    Debug.WriteLine("[CONFIG] ✗ Cannot save config: connection is null");
                    return;
                }

                if (string.IsNullOrWhiteSpace(server))
                {
                    Logging.Log("Config save error: Server URL is empty");
                    Debug.WriteLine("[CONFIG] ✗ Cannot save config: server URL is empty");
                    return;
                }

                Debug.WriteLine($"[CONFIG] Attempting to save server: {server}");

                // Check if record exists
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM config";
                var count = (long)checkCommand.ExecuteScalar();
                Debug.WriteLine($"[CONFIG] Existing config rows: {count}");

                using var command = connection.CreateCommand();
                if (count > 0)
                {
                    command.CommandText = "UPDATE config SET server = @server WHERE id = 1";
                    Debug.WriteLine("[CONFIG] Executing UPDATE");
                }
                else
                {
                    command.CommandText = "INSERT INTO config (id, server) VALUES (1, @server)";
                    Debug.WriteLine("[CONFIG] Executing INSERT");
                }

                command.Parameters.AddWithValue("@server", server);
                var rowsAffected = command.ExecuteNonQuery();
                Debug.WriteLine($"[CONFIG] ✓ Rows affected: {rowsAffected}");

                // Verify the save by querying immediately
                using var verifyCommand = connection.CreateCommand();
                verifyCommand.CommandText = "SELECT server FROM config WHERE id = 1";
                var savedServer = verifyCommand.ExecuteScalar()?.ToString();
                if (savedServer == server)
                {
                    Debug.WriteLine($"[CONFIG] ✓ Server verified in database: {server}");
                    Logging.Log($"Server endpoint saved successfully: {server}");
                }
                else
                {
                    Debug.WriteLine($"[CONFIG] ✗ Server verification failed. Saved: {savedServer}, Expected: {server}");
                    Logging.Log($"Server endpoint may not have saved correctly");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[CONFIG] ✗ SaveConfig exception: {e.Message}");
                Debug.WriteLine($"[CONFIG] Stack trace: {e.StackTrace}");
                Logging.Log($"Config save error: {e.Message}");
            }
        }

        private string GetTokenForServer()
        {
            try
            {
                if (connection == null || string.IsNullOrWhiteSpace(serverImpl))
                    return "";

                var baseUrl = ExtractBaseUrl(serverImpl);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT oauth2_token FROM server_tokens WHERE base_url = @base_url LIMIT 1";
                command.Parameters.AddWithValue("@base_url", baseUrl);

                var result = command.ExecuteScalar();
                if (result != null)
                {
                    var token = result.ToString();
                    Debug.WriteLine($"[CONFIG] Loaded token for server: {baseUrl}");
                    return token;
                }

                Debug.WriteLine($"[CONFIG] No token found for server: {baseUrl}");
                return "";
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[CONFIG] Error retrieving server token: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// Saves token and expiry time as atomic operation.
        /// Prevents data loss from INSERT OR REPLACE truncating unspecified columns.
        /// </summary>
        private void SaveTokenAndExpiry(string token, long expiresAt)
        {
            try
            {
                if (connection == null || string.IsNullOrWhiteSpace(serverImpl))
                {
                    Logging.Log("Config save error: Database not initialized or server not set");
                    return;
                }

                var baseUrl = ExtractBaseUrl(serverImpl);
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO server_tokens (base_url, oauth2_token, token_expires_at)
                    VALUES (@base_url, @token, @expiresAt)";
                command.Parameters.AddWithValue("@base_url", baseUrl);
                command.Parameters.AddWithValue("@token", token ?? "");
                command.Parameters.AddWithValue("@expiresAt", expiresAt);
                command.ExecuteNonQuery();

                tokenImpl = token;
                tokenExpiresAtImpl = expiresAt;
                Debug.WriteLine($"[CONFIG] ✓ Token and expiry saved for server: {baseUrl} (expires: {expiresAt})");
            }
            catch (Exception e)
            {
                Logging.Log($"Config save error: {e.Message}");
            }
        }

        private static string ExtractBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            try
            {
                var uri = new Uri(url.StartsWith("http") ? url : $"http://{url}");
                return $"{uri.Scheme}://{uri.Host}";
            }
            catch
            {
                return url;
            }
        }
    }
}
