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
    private readonly IDatabaseService _databaseService;
    private readonly IMetadataService _metadataService;
    private readonly IGoogleDriveService _driveService;

    public ObservableCollection<Playlist> Playlists { get; } = [];
    public ObservableCollection<AlbumInfo> Albums { get; } = [];

    [ObservableProperty]
    private int _favoritesCount;

    [ObservableProperty]
    private bool _hasAlbums;

    [ObservableProperty]
    private bool _isScanningMetadata;

    public PlaylistsViewModel(
        IPlaylistService playlistService,
        IFavoritesService favoritesService,
        IDatabaseService databaseService,
        IMetadataService metadataService,
        IGoogleDriveService driveService)
    {
        _playlistService = playlistService;
        _favoritesService = favoritesService;
        _databaseService = databaseService;
        _metadataService = metadataService;
        _driveService = driveService;
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

            // Show albums immediately from current DB state
            await RefreshAlbumsAsync();

            // Background: scan metadata for existing tracks only (no Drive API calls)
            var savedFolderIds = LibraryViewModel.GetSavedFolders().Select(f => f.Id).ToList();
            var pending = await _databaseService.GetTracksWithDefaultMetadataAsync(savedFolderIds);
            if (pending.Count > 0)
                _ = ScanMetadataInBackgroundAsync(pending);
        }, "Failed to load playlists");
    }

    private void RefreshAlbums(List<AlbumInfo> albums)
    {
        Albums.Clear();
        foreach (var album in albums)
            Albums.Add(album);
        HasAlbums = Albums.Count > 0;
        IsEmpty = Playlists.Count == 0 && FavoritesCount == 0 && Albums.Count == 0;
        EmptyMessage = "No playlists yet. Create one!";
    }

    private async Task RefreshAlbumsAsync()
    {
        var folderIds = LibraryViewModel.GetSavedFolders().Select(f => f.Id).ToList();
        var albums = folderIds.Count > 0
            ? await _databaseService.GetAlbumsAsync(folderIds)
            : [];
        RefreshAlbums(albums);
    }

    private async Task ScanMetadataInBackgroundAsync(List<AudioTrack> tracks)
    {
        try
        {
            IsScanningMetadata = true;
            System.Diagnostics.Debug.WriteLine($"[MetaScan] Starting scan for {tracks.Count} tracks with default metadata");

            await ScanMetadataBatchAsync(tracks);

            // Always refresh albums after scan (partial updates may exist from previous sessions)
            var folderIds = LibraryViewModel.GetSavedFolders().Select(f => f.Id).ToList();
            var albums = folderIds.Count > 0
                ? await _databaseService.GetAlbumsAsync(folderIds)
                : [];
            System.Diagnostics.Debug.WriteLine($"[MetaScan] Post-scan album count: {albums.Count}");
            foreach (var a in albums)
                System.Diagnostics.Debug.WriteLine($"[MetaScan]   Album: \"{a.Name}\" by \"{a.Artist}\" ({a.TrackCount} tracks)");

            MainThread.BeginInvokeOnMainThread(() => RefreshAlbums(albums));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaScan] Background scan failed: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsScanningMetadata = false);
        }
    }

    private async Task ScanMetadataBatchAsync(List<AudioTrack> tracks)
    {
        const int maxConcurrency = 4;
        var successCount = 0;
        var failCount = 0;

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = tracks.Select(async track =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (await ScanSingleTrackAsync(track))
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        System.Diagnostics.Debug.WriteLine($"[MetaScan] Batch complete: {successCount} succeeded, {failCount} failed out of {tracks.Count}");
    }

    private async Task<bool> ScanSingleTrackAsync(AudioTrack track)
    {
        try
        {
            // Try cached file first (no network needed)
            var localPath = track.CachedFilePath;
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                System.Diagnostics.Debug.WriteLine($"[MetaScan] {track.FileName}: using cached file");
                var oldAlbum = track.Album;
                await ExtractAndSaveMetadataAsync(track, localPath);
                System.Diagnostics.Debug.WriteLine($"[MetaScan] {track.FileName}: \"{oldAlbum}\" -> \"{track.Album}\" by \"{track.Artist}\"");
                return true;
            }

            // Partial download for metadata extraction
            var tempPath = await _driveService.DownloadPartialToTempAsync(
                track.DriveFileId, track.FileExtension);
            if (tempPath == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MetaScan] {track.FileName}: partial download returned null");
                return false;
            }

            try
            {
                var oldAlbum = track.Album;
                await ExtractAndSaveMetadataAsync(track, tempPath);
                System.Diagnostics.Debug.WriteLine($"[MetaScan] {track.FileName}: \"{oldAlbum}\" -> \"{track.Album}\" by \"{track.Artist}\"");
                return true;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaScan] {track.FileName}: FAILED - {ex.Message}");
            return false;
        }
    }

    private async Task ExtractAndSaveMetadataAsync(AudioTrack track, string filePath)
    {
        await _metadataService.ExtractMetadataAsync(filePath, track);
        if (string.IsNullOrEmpty(track.AlbumArtPath))
            track.AlbumArtPath = await _metadataService.ExtractAlbumArtAsync(filePath, track.DriveFileId);
        await _databaseService.SaveTrackAsync(track);
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
    private async Task OpenAlbumAsync(AlbumInfo album)
    {
        await Shell.Current.GoToAsync($"albumdetail?albumName={Uri.EscapeDataString(album.Name)}&artistName={Uri.EscapeDataString(album.Artist)}");
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
