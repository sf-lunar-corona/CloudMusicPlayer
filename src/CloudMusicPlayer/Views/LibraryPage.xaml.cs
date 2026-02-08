using CloudMusicPlayer.Services;
using CloudMusicPlayer.Services.Interfaces;
using CloudMusicPlayer.ViewModels;

namespace CloudMusicPlayer.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;
    private readonly IAudioPlaybackService _playbackService;
    private bool _mediaElementConnected;

    public LibraryPage(LibraryViewModel viewModel, IAudioPlaybackService playbackService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _playbackService = playbackService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_mediaElementConnected && _playbackService is AudioPlaybackService service)
        {
            service.SetMediaElement(GlobalMediaElement);
            _mediaElementConnected = true;
            System.Diagnostics.Debug.WriteLine("[LibraryPage] MediaElement connected to playback service");
        }

        await _viewModel.LoadFoldersCommand.ExecuteAsync(null);
    }
}
