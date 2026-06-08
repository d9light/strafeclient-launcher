using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
// removed using statements
namespace StrafeClient
{
    public class AccountInfo
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Type { get; set; } // "Offline" ou "Microsoft"
        public string Token { get; set; }
        public bool IsMicrosoft { get; set; }
        public string UUID { get; set; }
    }

    public class AccountData
    {
        public List<AccountInfo> Accounts { get; set; } = new List<AccountInfo>();
        public string ActiveAccountId { get; set; }
    }

    public class AccountManager
    {
        private static readonly string AccountsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StrafeClient",
            "accounts.json"
        );

        private static AccountData data;

        static AccountManager()
        {
            LoadAccounts();
        }

        public static void LoadAccounts()
        {
            if (File.Exists(AccountsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(AccountsFilePath);
                    data = JsonSerializer.Deserialize<AccountData>(json) ?? new AccountData();
                    // [SECURITY FIX HIGH-1] Decrypt tokens after loading
                    foreach (var acc in data.Accounts)
                    {
                        acc.Token = DecryptToken(acc.Token ?? "");
                    }
                }
                catch
                {
                    data = new AccountData();
                }
            }
            else
            {
                data = new AccountData();
            }
        }

        public static void SaveAccounts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AccountsFilePath));
            
            // [SECURITY FIX HIGH-1] Encrypt tokens using Windows DPAPI before saving.
            // DPAPI binds encryption to the current Windows user — only this user can decrypt.
            var toSave = new AccountData
            {
                ActiveAccountId = data.ActiveAccountId,
                Accounts = new List<AccountInfo>()
            };
            foreach (var acc in data.Accounts)
            {
                toSave.Accounts.Add(new AccountInfo
                {
                    Id = acc.Id,
                    Username = acc.Username,
                    Type = acc.Type,
                    IsMicrosoft = acc.IsMicrosoft,
                    UUID = acc.UUID,
                    // Encrypt non-empty tokens with DPAPI
                    Token = string.IsNullOrEmpty(acc.Token) ? "" : EncryptToken(acc.Token)
                });
            }
            
            string json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AccountsFilePath, json);
        }

        // [SECURITY] Encrypt a plaintext token using Windows DPAPI (per-user scope)
        private static string EncryptToken(string plaintext)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plaintext);
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                // Fallback: save plaintext if DPAPI fails (e.g., running as service)
                return plaintext;
            }
        }

        // [SECURITY] Decrypt a DPAPI-encrypted token back to plaintext
        private static string DecryptToken(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return "";
            try
            {
                byte[] encrypted = Convert.FromBase64String(ciphertext);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // If decryption fails, the token might be plaintext from an old version
                return ciphertext;
            }
        }

        public static List<AccountInfo> GetAccounts()
        {
            return data.Accounts;
        }

        public static AccountInfo GetActiveAccount()
        {
            if (string.IsNullOrEmpty(data.ActiveAccountId)) return null;
            return data.Accounts.FirstOrDefault(a => a.Id == data.ActiveAccountId);
        }

        public static void SetActiveAccount(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                data.ActiveAccountId = null;
                SaveAccounts();
                return;
            }
            var account = data.Accounts.FirstOrDefault(a => a.Id == id);
            if (account != null)
            {
                data.ActiveAccountId = id;
                SaveAccounts();
            }
        }

        public static void Logout()
        {
            if (!string.IsNullOrEmpty(data.ActiveAccountId))
            {
                data.Accounts.RemoveAll(a => a.Id == data.ActiveAccountId);
                data.ActiveAccountId = null;
                SaveAccounts();
            }
        }

        public static void AddOfflineAccount(string username)
        {
            // Check if already exists
            var existing = data.Accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && a.Type == "Offline");
            if (existing == null)
            {
                string newId = Guid.NewGuid().ToString();
                data.Accounts.Add(new AccountInfo
                {
                    Id = newId,
                    Username = username,
                    Type = "Offline"
                });
                data.ActiveAccountId = newId; // Make it active by default
                SaveAccounts();
            }
        }

        public static void AddStrafeAccount(string username, string token = "")
        {
            // Check if already exists
            var existing = data.Accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && a.Type == "StrafeAPI");
            if (existing == null)
            {
                string newId = Guid.NewGuid().ToString();
                data.Accounts.Add(new AccountInfo
                {
                    Id = newId,
                    Username = username,
                    Type = "StrafeAPI", // Representa a conta premium do próprio launcher
                    Token = token
                });
                data.ActiveAccountId = newId;
            }
            else
            {
                existing.Token = token;
                data.ActiveAccountId = existing.Id;
            }
            SaveAccounts();
        }

        public static void DeleteAccount(string id)
        {
            data.Accounts.RemoveAll(a => a.Id == id);
            if (data.ActiveAccountId == id)
            {
                data.ActiveAccountId = data.Accounts.FirstOrDefault()?.Id;
            }
            SaveAccounts();
        }

        public static async Task<MSession> LoginMicrosoftAsync(string authCode)
        {
            return await MicrosoftAuthHelper.AuthenticateWithAuthCode(authCode);
        }
    }
}
