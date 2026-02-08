using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class SearchPage : ContentPage
{
    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
