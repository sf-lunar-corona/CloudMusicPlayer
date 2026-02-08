using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class NowPlayingViewModel : BaseViewModel
{
    private readonly IAudioPlaybackService _playbackService;
    private readonly IFavoritesService _favoritesService;

    [ObservableProperty]
    private AudioTrack? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _position;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private RepeatMode _repeatMode;

    [ObservableProperty]
    private string _repeatModeIcon = "↻";

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private string? _albumArtSource;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _positionText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    [ObservableProperty]
    private int _queueCount;

    [ObservableProperty]
    private int _currentQueuePosition;

    [ObservableProperty]
    private bool _isLoading;

    public NowPlayingViewModel(IAudioPlaybackService playbackService, IFavoritesService favoritesService)
    {
        _playbackService = playbackService;
        _favoritesService = favoritesService;
        Title = "Now Playing";

        _playbackService.TrackChanged += OnTrackChanged;
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.LoadingStateChanged += OnLoadingStateChanged;
    }

    private void OnTrackChanged(object? sender, AudioTrack? track)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentTrack = track;
            AlbumArtSource = track?.AlbumArtPath;
            IsFavorite = track?.IsFavorite ?? false;
            QueueCount = _playbackService.Queue.Count;
            CurrentQueuePosition = _playbackService.CurrentIndex + 1;
        });
    }

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = isPlaying;
        });
    }

    private void OnLoadingStateChanged(object? sender, bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsLoading = isLoading;
        });
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Position = position;
            Duration = _playbackService.Duration;

            PositionText = FormatTime(position);
            DurationText = FormatTime(Duration);

            if (Duration.TotalSeconds > 0)
                Progress = position.TotalSeconds / Duration.TotalSeconds;
        });
    }

    [RelayCommand]
    private void LoadCurrentState()
    {
        CurrentTrack = _playbackService.CurrentTrack;
        IsPlaying = _playbackService.IsPlaying;
        IsShuffleEnabled = _playbackService.IsShuffleEnabled;
        RepeatMode = _playbackService.RepeatMode;
        Volume = _playbackService.Volume;
        IsLoading = _playbackService.IsLoading;
        AlbumArtSource = CurrentTrack?.AlbumArtPath;
        IsFavorite = CurrentTrack?.IsFavorite ?? false;
        QueueCount = _playbackService.Queue.Count;
        CurrentQueuePosition = _playbackService.CurrentIndex + 1;
        UpdateRepeatModeIcon();
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (IsPlaying)
            await _playbackService.PauseAsync();
        else
            await _playbackService.ResumeAsync();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await _playbackService.NextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await _playbackService.PreviousAsync();
    }

    [RelayCommand]
    private async Task SeekAsync(double value)
    {
        var newPosition = TimeSpan.FromSeconds(value * Duration.TotalSeconds);
        await _playbackService.SeekAsync(newPosition);
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffleEnabled = !IsShuffleEnabled;
        _playbackService.IsShuffleEnabled = IsShuffleEnabled;
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.None,
            _ => RepeatMode.None
        };
        _playbackService.RepeatMode = RepeatMode;
        UpdateRepeatModeIcon();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentTrack == null) return;
        await _favoritesService.ToggleFavoriteAsync(CurrentTrack);
        IsFavorite = CurrentTrack.IsFavorite;
    }

    [RelayCommand]
    private void ChangeVolume(double value)
    {
        Volume = value;
        _playbackService.Volume = value;
    }

    private void UpdateRepeatModeIcon()
    {
        RepeatModeIcon = RepeatMode switch
        {
            RepeatMode.All => "🔁",
            RepeatMode.One => "🔂",
            _ => "↻"
        };
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }
}
