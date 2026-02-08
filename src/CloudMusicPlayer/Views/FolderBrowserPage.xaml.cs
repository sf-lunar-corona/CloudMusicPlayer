using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class FolderBrowserPage : ContentPage
{
    private readonly FolderBrowserViewModel _viewModel;

    public FolderBrowserPage(FolderBrowserViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFolderCommand.ExecuteAsync(null);
    }
}
