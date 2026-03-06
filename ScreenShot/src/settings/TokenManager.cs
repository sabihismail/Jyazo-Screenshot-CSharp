using System;
using System.Data.SQLite;
using System.Diagnostics;
using ScreenShot.src.tools;

namespace ScreenShot.src.settings
{
    public class TokenManager
    {
        private readonly SQLiteConnection _connection;

        public TokenManager(SQLiteConnection connection)
        {
            _connection = connection;
        }

        public string GetToken()
        {
            try
            {
                if (_connection == null)
                    return "";

                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT oauth2_token FROM config LIMIT 1";

                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[TOKEN] Error retrieving token: {e.Message}");
                return "";
            }
        }

        public void SaveToken(string token)
        {
            try
            {
                if (_connection == null)
                {
                    Logging.Log("Token save error: Database not initialized");
                    return;
                }

                // Save to config table
                using var command = _connection.CreateCommand();
                command.CommandText = "UPDATE config SET oauth2_token = @token WHERE id = 1";
                command.Parameters.AddWithValue("@token", token ?? "");
                command.ExecuteNonQuery();

                Debug.WriteLine($"[TOKEN] ✓ Token saved");
            }
            catch (Exception e)
            {
                Logging.Log($"Token save error: {e.Message}");
            }
        }

        public string GetTokenForUrl(string url)
        {
            try
            {
                if (_connection == null || string.IsNullOrWhiteSpace(url))
                    return "";

                var baseUrl = ExtractBaseUrl(url);
                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT oauth2_token FROM server_tokens WHERE base_url = @base_url LIMIT 1";
                command.Parameters.AddWithValue("@base_url", baseUrl);

                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[TOKEN] Error retrieving token for URL: {e.Message}");
                return "";
            }
        }

        public void SaveTokenForUrl(string url, string token)
        {
            try
            {
                if (_connection == null || string.IsNullOrWhiteSpace(url))
                    return;

                var baseUrl = ExtractBaseUrl(url);
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO server_tokens (base_url, oauth2_token)
                    VALUES (@base_url, @token)
                ";
                command.Parameters.AddWithValue("@base_url", baseUrl);
                command.Parameters.AddWithValue("@token", token ?? "");
                command.ExecuteNonQuery();

                Debug.WriteLine($"[TOKEN] Token saved for URL: {baseUrl}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[TOKEN] Error saving token for URL: {e.Message}");
            }
        }

        private static string ExtractBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            try
            {
                var uri = new Uri(url.StartsWith("http") ? url : $"http://{url}");
                return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            }
            catch
            {
                return url;
            }
        }
    }
}
