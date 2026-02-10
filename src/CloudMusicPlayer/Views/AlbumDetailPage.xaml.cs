using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class AlbumDetailPage : ContentPage
{
    public AlbumDetailPage(AlbumDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
