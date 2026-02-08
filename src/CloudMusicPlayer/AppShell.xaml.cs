using CloudMusicPlayer.Views;

namespace CloudMusicPlayer;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for pages that aren't in tabs
        Routing.RegisterRoute("folderbrowser", typeof(FolderBrowserPage));
        Routing.RegisterRoute("nowplaying", typeof(NowPlayingPage));
        Routing.RegisterRoute("playlistdetail", typeof(PlaylistDetailPage));
        Routing.RegisterRoute("equalizer", typeof(EqualizerPage));
    }
}
