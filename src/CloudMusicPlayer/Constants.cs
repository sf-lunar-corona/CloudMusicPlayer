namespace CloudMusicPlayer;

public static class Constants
{
    // Replace with your actual Google Cloud Console credentials
    // Note: Android uses the "iOS" type OAuth client (supports custom URI schemes)
    public const string GoogleClientIdAndroid = "436318736481-s6dq9f696n6vqm6vgskh9cq6g8ng4fus.apps.googleusercontent.com";
    public const string GoogleClientIdIos = "436318736481-s6dq9f696n6vqm6vgskh9cq6g8ng4fus.apps.googleusercontent.com";
    public const string GoogleClientIdWindows = "YOUR_WINDOWS_CLIENT_ID.apps.googleusercontent.com";

    public const string GoogleRedirectUriWindows = "http://localhost:8642/oauth2callback";

    // Android redirect URI uses reverse client ID
    public static string GoogleRedirectUriAndroid => $"com.googleusercontent.apps.{GoogleClientIdAndroid.Split('.')[0]}:/oauth2callback";

    // iOS redirect URI uses reverse client ID
    public static string GoogleRedirectUriIos => $"com.googleusercontent.apps.{GoogleClientIdIos.Split('.')[0]}:/oauth2callback";

    public static readonly string[] GoogleScopes =
    [
        "https://www.googleapis.com/auth/drive.readonly",
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/userinfo.profile"
    ];

    public const string DatabaseFilename = "cloudmusicplayer.db3";
    public const long DefaultCacheSizeLimitBytes = 2L * 1024 * 1024 * 1024; // 2GB

    public static readonly string[] SupportedAudioExtensions =
    [
        ".mp3", ".aac", ".m4a", ".wav", ".flac", ".ogg", ".wma", ".opus", ".alac"
    ];

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

    public static string CacheDirectory =>
        Path.Combine(FileSystem.CacheDirectory, "MusicCache");
}
