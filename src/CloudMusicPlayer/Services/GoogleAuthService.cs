using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudMusicPlayer.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;

namespace CloudMusicPlayer.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private UserCredential? _credential;
    private const string TokenKey = "google_auth_token";

    private static string ClientId =>
#if ANDROID
        Constants.GoogleClientIdAndroid;
#elif IOS
        Constants.GoogleClientIdIos;
#else
        Constants.GoogleClientIdWindows;
#endif

    private static string RedirectUri =>
#if ANDROID
        Constants.GoogleRedirectUriAndroid;
#elif IOS
        Constants.GoogleRedirectUriIos;
#else
        Constants.GoogleRedirectUriWindows;
#endif

    public async Task<UserCredential?> SignInAsync()
    {
#if WINDOWS
        return await SignInWindowsAsync();
#else
        return await SignInMobileAsync();
#endif
    }

    private async Task<UserCredential?> SignInMobileAsync()
    {
#if ANDROID || IOS
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(" ", Constants.GoogleScopes))}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&access_type=offline" +
            $"&prompt=consent";

        System.Diagnostics.Debug.WriteLine($"[Auth] Redirect URI: {RedirectUri}");
        System.Diagnostics.Debug.WriteLine($"[Auth] Auth URL: {authUrl}");

        var result = await WebAuthenticator.AuthenticateAsync(
            new WebAuthenticatorOptions
            {
                Url = new Uri(authUrl),
                CallbackUrl = new Uri(RedirectUri),
                PrefersEphemeralWebBrowserSession = true
            });

        System.Diagnostics.Debug.WriteLine($"[Auth] WebAuthenticator returned. Properties: {string.Join(", ", result?.Properties?.Select(kv => $"{kv.Key}={kv.Value}") ?? [])}");

        var code = result?.Properties?.GetValueOrDefault("code");
        if (string.IsNullOrEmpty(code))
        {
            System.Diagnostics.Debug.WriteLine("[Auth] No authorization code received");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[Auth] Got code, exchanging for token...");
        _credential = await ExchangeCodeForTokenAsync(code, codeVerifier, RedirectUri);
        await SaveTokenAsync();
        return _credential;
#else
        return null;
#endif
    }

    private async Task<UserCredential?> SignInWindowsAsync()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var redirectUri = Constants.GoogleRedirectUriWindows;

        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(" ", Constants.GoogleScopes))}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&access_type=offline" +
            $"&prompt=consent";

        var code = await ListenForOAuthCallbackAsync(authUrl, redirectUri);
        if (string.IsNullOrEmpty(code)) return null;

        _credential = await ExchangeCodeForTokenAsync(code, codeVerifier, redirectUri);
        await SaveTokenAsync();
        return _credential;
    }

    private static async Task<string?> ListenForOAuthCallbackAsync(string authUrl, string redirectUri)
    {
        using var listener = new HttpListener();
        var listenerPrefix = redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";
        listener.Prefixes.Add(listenerPrefix);
        listener.Start();

        await Browser.OpenAsync(new Uri(authUrl), BrowserLaunchMode.SystemPreferred);

        var context = await listener.GetContextAsync();
        var code = context.Request.QueryString["code"];

        var response = context.Response;
        var responseString = "<html><body><h2>Authentication successful!</h2><p>You can close this window and return to the app.</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
        listener.Stop();

        return code;
    }

    private async Task<UserCredential> ExchangeCodeForTokenAsync(string code, string codeVerifier, string redirectUri)
    {
        using var client = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = ClientId,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };

        System.Diagnostics.Debug.WriteLine($"[Auth] Token exchange - client_id: {ClientId}");
        System.Diagnostics.Debug.WriteLine($"[Auth] Token exchange - redirect_uri: {redirectUri}");

        var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(tokenRequest));

        var json = await response.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"[Auth] Token response ({response.StatusCode}): {json}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Token exchange failed ({response.StatusCode}): {json}");
        }

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(json);

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = ClientId }
        });

        var token = new TokenResponse
        {
            AccessToken = tokenResponse.GetProperty("access_token").GetString(),
            RefreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresInSeconds = tokenResponse.GetProperty("expires_in").GetInt64(),
            IssuedUtc = DateTime.UtcNow
        };

        return new UserCredential(flow, "user", token);
    }

    public async Task SignOutAsync()
    {
        _credential = null;
        try
        {
            SecureStorage.Remove(TokenKey);
        }
        catch { }
        await Task.CompletedTask;
    }

    public async Task<UserCredential?> GetCurrentCredentialAsync()
    {
        if (_credential != null)
        {
            if (_credential.Token.IsStale)
            {
                System.Diagnostics.Debug.WriteLine("[Auth] Token is stale, refreshing...");
                var refreshed = await ManualRefreshTokenAsync();
                if (!refreshed)
                {
                    System.Diagnostics.Debug.WriteLine("[Auth] Token refresh failed");
                    _credential = null;
                    return null;
                }
                System.Diagnostics.Debug.WriteLine("[Auth] Token refreshed successfully");
            }
            return _credential;
        }

        return await TryRestoreTokenAsync();
    }

    private async Task<bool> ManualRefreshTokenAsync()
    {
        if (_credential?.Token?.RefreshToken == null)
        {
            System.Diagnostics.Debug.WriteLine("[Auth] No refresh token available");
            return false;
        }

        try
        {
            using var client = new HttpClient();
            var refreshRequest = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["refresh_token"] = _credential.Token.RefreshToken,
                ["grant_type"] = "refresh_token"
            };

            var response = await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(refreshRequest));

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[Auth] Refresh response ({response.StatusCode}): {json}");

            if (!response.IsSuccessStatusCode) return false;

            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = ClientId }
            });

            var token = new TokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString(),
                RefreshToken = _credential.Token.RefreshToken, // Keep existing refresh token
                ExpiresInSeconds = tokenData.GetProperty("expires_in").GetInt64(),
                IssuedUtc = DateTime.UtcNow
            };

            _credential = new UserCredential(flow, "user", token);
            await SaveTokenAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Manual refresh failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ForceRefreshTokenAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Auth] ForceRefreshTokenAsync called");
        if (_credential?.Token?.RefreshToken == null)
        {
            System.Diagnostics.Debug.WriteLine("[Auth] No credential/refresh token for force refresh");
            return false;
        }

        var result = await ManualRefreshTokenAsync();
        System.Diagnostics.Debug.WriteLine($"[Auth] Force refresh result: {result}");
        return result;
    }

    public async Task<bool> IsSignedInAsync()
    {
        var credential = await GetCurrentCredentialAsync();
        return credential != null;
    }

    public async Task<string?> GetUserEmailAsync()
    {
        var credential = await GetCurrentCredentialAsync();
        if (credential == null) return null;

        try
        {
            var service = new Oauth2Service(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });
            var userInfo = await service.Userinfo.Get().ExecuteAsync();
            return userInfo.Email;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetUserNameAsync()
    {
        var credential = await GetCurrentCredentialAsync();
        if (credential == null) return null;

        try
        {
            var service = new Oauth2Service(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });
            var userInfo = await service.Userinfo.Get().ExecuteAsync();
            return userInfo.Name;
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveTokenAsync()
    {
        if (_credential?.Token == null) return;

        try
        {
            var tokenJson = JsonSerializer.Serialize(new
            {
                _credential.Token.AccessToken,
                _credential.Token.RefreshToken,
                _credential.Token.ExpiresInSeconds,
                IssuedUtc = _credential.Token.IssuedUtc.ToString("O")
            });
            await SecureStorage.SetAsync(TokenKey, tokenJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save token: {ex}");
        }
    }

    private async Task<UserCredential?> TryRestoreTokenAsync()
    {
        try
        {
            var tokenJson = await SecureStorage.GetAsync(TokenKey);
            if (string.IsNullOrEmpty(tokenJson)) return null;

            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = ClientId }
            });

            var token = new TokenResponse
            {
                AccessToken = tokenData.GetProperty("AccessToken").GetString(),
                RefreshToken = tokenData.TryGetProperty("RefreshToken", out var rt) ? rt.GetString() : null,
                ExpiresInSeconds = tokenData.GetProperty("ExpiresInSeconds").GetInt64(),
                IssuedUtc = DateTime.Parse(tokenData.GetProperty("IssuedUtc").GetString()!)
            };

            _credential = new UserCredential(flow, "user", token);

            if (_credential.Token.IsStale && !string.IsNullOrEmpty(_credential.Token.RefreshToken))
            {
                await _credential.RefreshTokenAsync(CancellationToken.None);
                await SaveTokenAsync();
            }

            return _credential;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore token: {ex}");
            return null;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
