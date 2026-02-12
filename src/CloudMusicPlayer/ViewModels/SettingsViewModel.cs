using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IGoogleAuthService _authService;
    private readonly ICacheService _cacheService;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _cacheSizeText = "Calculating...";

    [ObservableProperty]
    private long _cacheSizeLimit;

    [ObservableProperty]
    private string _cacheSizeLimitText = "2 GB";

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    public SettingsViewModel(
        IGoogleAuthService authService,
        ICacheService cacheService)
    {
        _authService = authService;
        _cacheService = cacheService;
        Title = "Settings";

        CacheSizeLimit = Preferences.Get("cache_size_limit", Constants.DefaultCacheSizeLimitBytes);
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        await ExecuteAsync(async () =>
        {
            UserEmail = await _authService.GetUserEmailAsync() ?? "Not signed in";
            UserName = await _authService.GetUserNameAsync() ?? "";

            var cacheSize = await _cacheService.GetCacheSizeAsync();
            CacheSizeText = FormatSize(cacheSize);

        }, "Failed to load settings");
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Clear Cache",
            "This will delete all cached music files. You'll need to re-download them for playback.",
            "Clear",
            "Cancel");

        if (confirm)
        {
            await _cacheService.ClearCacheAsync();
            CacheSizeText = "0 B";
        }
    }

    [RelayCommand]
    private async Task ChangeCacheLimitAsync()
    {
        var options = new[] { "500 MB", "1 GB", "2 GB", "5 GB", "10 GB" };
        var result = await Shell.Current.DisplayActionSheet("Cache Size Limit", "Cancel", null, options);

        if (result != null && result != "Cancel")
        {
            CacheSizeLimit = result switch
            {
                "500 MB" => 500L * 1024 * 1024,
                "1 GB" => 1L * 1024 * 1024 * 1024,
                "2 GB" => 2L * 1024 * 1024 * 1024,
                "5 GB" => 5L * 1024 * 1024 * 1024,
                "10 GB" => 10L * 1024 * 1024 * 1024,
                _ => Constants.DefaultCacheSizeLimitBytes
            };

            Preferences.Set("cache_size_limit", CacheSizeLimit);
            CacheSizeLimitText = result;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Sign Out",
            "Are you sure you want to sign out?",
            "Sign Out",
            "Cancel");

        if (confirm)
        {
            await _authService.SignOutAsync();
            await Shell.Current.GoToAsync("//login");
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
