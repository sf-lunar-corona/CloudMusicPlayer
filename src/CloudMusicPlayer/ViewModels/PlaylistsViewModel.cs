using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class PlaylistsViewModel : BaseViewModel
{
    private readonly IPlaylistService _playlistService;
    private readonly IFavoritesService _favoritesService;

    public ObservableCollection<Playlist> Playlists { get; } = [];

    [ObservableProperty]
    private int _favoritesCount;

    public PlaylistsViewModel(IPlaylistService playlistService, IFavoritesService favoritesService)
    {
        _playlistService = playlistService;
        _favoritesService = favoritesService;
        Title = "Playlists";
    }

    [RelayCommand]
    private async Task LoadPlaylistsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var playlists = await _playlistService.GetAllPlaylistsAsync();
            var favorites = await _favoritesService.GetFavoritesAsync();
            FavoritesCount = favorites.Count;

            Playlists.Clear();
            foreach (var playlist in playlists)
                Playlists.Add(playlist);

            IsEmpty = Playlists.Count == 0 && FavoritesCount == 0;
            EmptyMessage = "No playlists yet. Create one!";
        }, "Failed to load playlists");
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "New Playlist",
            "Enter playlist name:",
            "Create",
            "Cancel",
            "My Playlist");

        if (!string.IsNullOrWhiteSpace(name))
        {
            var playlist = await _playlistService.CreatePlaylistAsync(name);
            Playlists.Add(playlist);
        }
    }

    [RelayCommand]
    private async Task OpenPlaylistAsync(Playlist playlist)
    {
        await Shell.Current.GoToAsync($"playlistdetail?playlistId={playlist.Id}");
    }

    [RelayCommand]
    private async Task OpenFavoritesAsync()
    {
        await Shell.Current.GoToAsync("playlistdetail?playlistId=-1");
    }

    [RelayCommand]
    private async Task DeletePlaylistAsync(Playlist playlist)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Delete Playlist",
            $"Delete \"{playlist.Name}\"?",
            "Delete",
            "Cancel");

        if (confirm)
        {
            await _playlistService.DeletePlaylistAsync(playlist.Id);
            Playlists.Remove(playlist);
        }
    }
}
