using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Auth;

namespace StrafeClient
{
    public class MicrosoftAuthResult
    {
        public MSession Session { get; set; }
        public string RefreshToken { get; set; }
    }

    public class MicrosoftAuthHelper
    {
        private static readonly HttpClient client = new HttpClient();
        private const string ClientId = "00000000402b5328";

        static MicrosoftAuthHelper()
        {
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static string GetLoginUrl()
        {
            return $"https://login.live.com/oauth20_authorize.srf?client_id={ClientId}&response_type=code&redirect_uri=https://login.live.com/oauth20_desktop.srf&scope=XboxLive.signin%20offline_access";
        }

        public static async Task<MicrosoftAuthResult> AuthenticateWithAuthCode(string authCode)
        {
            // 1. Obter Token (Auth Code -> Access Token)
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", authCode),
                new KeyValuePair<string, string>("redirect_uri", "https://login.live.com/oauth20_desktop.srf")
            });

            var tokenRes = await client.PostAsync("https://login.live.com/oauth20_token.srf", tokenRequest);
            var responseString = await tokenRes.Content.ReadAsStringAsync();
            if (!tokenRes.IsSuccessStatusCode)
            {
                throw new Exception("Erro Auth Code: " + responseString);
            }
            var tokenJson = JsonDocument.Parse(responseString);
            string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
            string refreshToken = tokenJson.RootElement.GetProperty("refresh_token").GetString();

            var session = await AuthenticateWithMsaToken(accessToken);
            return new MicrosoftAuthResult { Session = session, RefreshToken = refreshToken };
        }

        public static async Task<MicrosoftAuthResult> RefreshMicrosoftTokenAsync(string refreshToken)
        {
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("redirect_uri", "https://login.live.com/oauth20_desktop.srf")
            });

            var tokenRes = await client.PostAsync("https://login.live.com/oauth20_token.srf", tokenRequest);
            var responseString = await tokenRes.Content.ReadAsStringAsync();
            if (!tokenRes.IsSuccessStatusCode)
            {
                throw new Exception("Erro Refresh Token: " + responseString);
            }
            var tokenJson = JsonDocument.Parse(responseString);
            string newAccessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
            string newRefreshToken = tokenJson.RootElement.GetProperty("refresh_token").GetString();

            var session = await AuthenticateWithMsaToken(newAccessToken);
            return new MicrosoftAuthResult { Session = session, RefreshToken = newRefreshToken };
        }

        public static async Task<MSession> AuthenticateWithMsaToken(string msaAccessToken)
        {
            // 3. XBL Auth
            var xblReq = new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = "d=" + msaAccessToken
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            };
            var xblContent = new StringContent(JsonSerializer.Serialize(xblReq), Encoding.UTF8, "application/json");

            var xblRes = await client.PostAsync("https://user.auth.xboxlive.com/user/authenticate", xblContent);
            var xblJson = JsonDocument.Parse(await xblRes.Content.ReadAsStringAsync());
            string xblToken = xblJson.RootElement.GetProperty("Token").GetString();
            string uhs = xblJson.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString();

            // 4. XSTS Auth
            var xstsReq = new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };
            var xstsContent = new StringContent(JsonSerializer.Serialize(xstsReq), Encoding.UTF8, "application/json");
            var xstsRes = await client.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", xstsContent);
            var xstsJson = JsonDocument.Parse(await xstsRes.Content.ReadAsStringAsync());
            string xstsToken = xstsJson.RootElement.GetProperty("Token").GetString();
            string xuid = xstsJson.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString();

            // 5. Minecraft Auth
            var mcReq = new
            {
                identityToken = "XBL3.0 x=" + xuid + ";" + xstsToken
            };
            var mcContent = new StringContent(JsonSerializer.Serialize(mcReq), Encoding.UTF8, "application/json");
            var mcRes = await client.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", mcContent);
            var mcJson = JsonDocument.Parse(await mcRes.Content.ReadAsStringAsync());
            string mcToken = mcJson.RootElement.GetProperty("access_token").GetString();

            // 6. Get Profile
            var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            profileReq.Headers.Add("Authorization", "Bearer " + mcToken);
            var profileRes = await client.SendAsync(profileReq);
            var profileJson = JsonDocument.Parse(await profileRes.Content.ReadAsStringAsync());
            string uuid = profileJson.RootElement.GetProperty("id").GetString();
            string name = profileJson.RootElement.GetProperty("name").GetString();
            
            // Format UUID with dashes as required by the game client
            if (uuid.Length == 32)
            {
                uuid = $"{uuid.Substring(0, 8)}-{uuid.Substring(8, 4)}-{uuid.Substring(12, 4)}-{uuid.Substring(16, 4)}-{uuid.Substring(20)}";
            }

            return new MSession
            {
                Username = name,
                AccessToken = mcToken,
                UUID = uuid
            };
        }
    }
}
