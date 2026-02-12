using CloudMusicPlayer.Services;
using CloudMusicPlayer.Services.Interfaces;
using CloudMusicPlayer.ViewModels;
using CloudMusicPlayer.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace CloudMusicPlayer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
        builder.Services.AddSingleton<IGoogleDriveService, GoogleDriveService>();
        builder.Services.AddSingleton<ICacheService, CacheService>();
        builder.Services.AddSingleton<IStreamingProxyService, StreamingProxyService>();
        builder.Services.AddSingleton<IMetadataService, MetadataService>();
        builder.Services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        builder.Services.AddSingleton<IPlaylistService, PlaylistService>();
        builder.Services.AddSingleton<IFavoritesService, FavoritesService>();

        // Platform-specific Equalizer
#if ANDROID
        builder.Services.AddSingleton<IEqualizerService, Platforms.Android.Services.AndroidEqualizerService>();
#elif IOS
        builder.Services.AddSingleton<IEqualizerService, Platforms.iOS.Services.iOSEqualizerService>();
#elif WINDOWS
        builder.Services.AddSingleton<IEqualizerService, Platforms.Windows.Services.WindowsEqualizerService>();
#endif

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<FolderBrowserViewModel>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<NowPlayingViewModel>();
        builder.Services.AddTransient<PlaylistsViewModel>();
        builder.Services.AddTransient<PlaylistDetailViewModel>();
        builder.Services.AddTransient<AlbumDetailViewModel>();
        builder.Services.AddTransient<EqualizerViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<FolderBrowserPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<NowPlayingPage>();
        builder.Services.AddTransient<PlaylistsPage>();
        builder.Services.AddTransient<PlaylistDetailPage>();
        builder.Services.AddTransient<AlbumDetailPage>();
        builder.Services.AddTransient<EqualizerPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
