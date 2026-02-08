using CloudMusicPlayer.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.Views;

public partial class NowPlayingPage : ContentPage
{
    private readonly NowPlayingViewModel _viewModel;

    public IRelayCommand BackCommand { get; }

    public NowPlayingPage(NowPlayingViewModel viewModel)
    {
        _viewModel = viewModel;
        BackCommand = new RelayCommand(async () => await Shell.Current.GoToAsync(".."));
        BindingContext = _viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCurrentStateCommand.Execute(null);
    }
}
