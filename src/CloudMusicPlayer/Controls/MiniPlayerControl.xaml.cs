using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Controls;

public partial class MiniPlayerControl : ContentView
{
    private IAudioPlaybackService? _playbackService;

    public MiniPlayerControl()
    {
        InitializeComponent();
        IsVisible = false;
    }

    public void Initialize(IAudioPlaybackService playbackService)
    {
        _playbackService = playbackService;
        _playbackService.TrackChanged += OnTrackChanged;
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;

        UpdateUI();
    }

    private void OnTrackChanged(object? sender, AudioTrack? track)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdateUI());
    }

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlayPauseButton.Text = isPlaying ? "⏸" : "▶";
        });
    }

    private void UpdateUI()
    {
        if (_playbackService?.CurrentTrack == null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        TitleLabel.Text = _playbackService.CurrentTrack.Title;
        ArtistLabel.Text = _playbackService.CurrentTrack.Artist;
        PlayPauseButton.Text = _playbackService.IsPlaying ? "⏸" : "▶";
    }

    private async void OnPlayPauseClicked(object? sender, EventArgs e)
    {
        if (_playbackService == null) return;

        if (_playbackService.IsPlaying)
            await _playbackService.PauseAsync();
        else
            await _playbackService.ResumeAsync();
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (_playbackService != null)
            await _playbackService.NextAsync();
    }

    private async void OnMiniPlayerTapped(object? sender, TappedEventArgs e)
    {
        if (_playbackService?.CurrentTrack != null)
            await Shell.Current.GoToAsync("nowplaying");
    }
}
