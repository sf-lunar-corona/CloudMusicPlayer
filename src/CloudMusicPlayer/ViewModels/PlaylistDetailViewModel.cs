using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

[QueryProperty(nameof(PlaylistId), "playlistId")]
public partial class PlaylistDetailViewModel : BaseViewModel
{
    private readonly IPlaylistService _playlistService;
    private readonly IFavoritesService _favoritesService;
    private readonly IAudioPlaybackService _playbackService;

    public ObservableCollection<AudioTrack> Tracks { get; } = [];

    [ObservableProperty]
    private int _playlistId;

    [ObservableProperty]
    private string _playlistName = string.Empty;

    [ObservableProperty]
    private bool _isFavoritesPlaylist;

    [ObservableProperty]
    private int _trackCount;

    public PlaylistDetailViewModel(
        IPlaylistService playlistService,
        IFavoritesService favoritesService,
        IAudioPlaybackService playbackService)
    {
        _playlistService = playlistService;
        _favoritesService = favoritesService;
        _playbackService = playbackService;
    }

    partial void OnPlaylistIdChanged(int value)
    {
        IsFavoritesPlaylist = value == -1;
        _ = LoadTracksAsync();
    }

    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        await ExecuteAsync(async () =>
        {
            List<AudioTrack> tracks;

            if (IsFavoritesPlaylist)
            {
                PlaylistName = "Favorites";
                Title = "Favorites";
                tracks = await _favoritesService.GetFavoritesAsync();
            }
            else
            {
                var playlist = await _playlistService.GetPlaylistAsync(PlaylistId);
                if (playlist == null) return;

                PlaylistName = playlist.Name;
                Title = playlist.Name;
                tracks = playlist.Tracks;
            }

            Tracks.Clear();
            foreach (var track in tracks)
                Tracks.Add(track);

            TrackCount = Tracks.Count;
            IsEmpty = Tracks.Count == 0;
            EmptyMessage = IsFavoritesPlaylist
                ? "No favorites yet. Tap the heart icon on any track."
                : "No tracks in this playlist yet.";
        }, "Failed to load tracks");
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

    [RelayCommand]
    private async Task RemoveTrackAsync(AudioTrack track)
    {
        if (IsFavoritesPlaylist)
        {
            await _favoritesService.ToggleFavoriteAsync(track);
        }
        else
        {
            await _playlistService.RemoveTrackFromPlaylistAsync(PlaylistId, track.Id);
        }
        Tracks.Remove(track);
        TrackCount = Tracks.Count;
    }
}
