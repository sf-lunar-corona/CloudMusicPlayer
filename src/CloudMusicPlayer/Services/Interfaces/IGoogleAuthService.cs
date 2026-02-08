using Google.Apis.Auth.OAuth2;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IGoogleAuthService
{
    Task<UserCredential?> SignInAsync();
    Task SignOutAsync();
    Task<UserCredential?> GetCurrentCredentialAsync();
    Task<bool> ForceRefreshTokenAsync();
    Task<bool> IsSignedInAsync();
    Task<string?> GetUserEmailAsync();
    Task<string?> GetUserNameAsync();
}
