using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

[QueryProperty(nameof(AlbumName), "albumName")]
[QueryProperty(nameof(ArtistName), "artistName")]
public partial class AlbumDetailViewModel : BaseViewModel
{
    private readonly IDatabaseService _databaseService;
    private readonly IAudioPlaybackService _playbackService;

    public ObservableCollection<AudioTrack> Tracks { get; } = [];

    [ObservableProperty]
    private string _albumName = string.Empty;

    [ObservableProperty]
    private string _artistName = string.Empty;

    [ObservableProperty]
    private int _trackCount;

    public AlbumDetailViewModel(
        IDatabaseService databaseService,
        IAudioPlaybackService playbackService)
    {
        _databaseService = databaseService;
        _playbackService = playbackService;
    }

    partial void OnAlbumNameChanged(string value)
    {
        Title = value;
        TryLoadTracks();
    }

    partial void OnArtistNameChanged(string value)
    {
        TryLoadTracks();
    }

    private void TryLoadTracks()
    {
        if (!string.IsNullOrEmpty(AlbumName) && !string.IsNullOrEmpty(ArtistName))
            _ = LoadTracksAsync();
    }

    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        await ExecuteAsync(async () =>
        {
            var tracks = await _databaseService.GetTracksByAlbumAsync(ArtistName, AlbumName);

            Tracks.Clear();
            foreach (var track in tracks)
                Tracks.Add(track);

            TrackCount = Tracks.Count;
            IsEmpty = Tracks.Count == 0;
            EmptyMessage = "No tracks in this album.";
        }, "Failed to load album tracks");
    }

    [RelayCommand]
    private async Task PlayTrackAsync(AudioTrack track)
    {
        var allTracks = Tracks.ToList();
        var index = allTracks.IndexOf(track);
        await _playbackService.PlayAsync(allTracks, index);
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Tracks.Count == 0) return;
        await _playbackService.PlayAsync(Tracks.ToList());
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task ShufflePlayAsync()
    {
        if (Tracks.Count == 0) return;
        _playbackService.IsShuffleEnabled = true;
        await _playbackService.PlayAsync(Tracks.ToList());
        await Shell.Current.GoToAsync("nowplaying");
    }
}
