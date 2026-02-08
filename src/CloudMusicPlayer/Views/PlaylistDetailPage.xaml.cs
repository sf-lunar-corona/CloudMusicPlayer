using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class PlaylistDetailPage : ContentPage
{
    public PlaylistDetailPage(PlaylistDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
