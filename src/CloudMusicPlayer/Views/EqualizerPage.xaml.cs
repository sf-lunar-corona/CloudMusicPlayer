using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class EqualizerPage : ContentPage
{
    private readonly EqualizerViewModel _viewModel;

    public EqualizerPage(EqualizerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
