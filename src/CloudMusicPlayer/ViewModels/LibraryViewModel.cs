using System.Collections.ObjectModel;
using System.Text.Json;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class LibraryViewModel : BaseViewModel
{
    private readonly IDatabaseService _databaseService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ICacheService _cacheService;

    public ObservableCollection<SavedFolder> Folders { get; } = [];

    [ObservableProperty]
    private int _folderCount;

    public LibraryViewModel(
        IDatabaseService databaseService,
        IAudioPlaybackService playbackService,
        ICacheService cacheService)
    {
        _databaseService = databaseService;
        _playbackService = playbackService;
        _cacheService = cacheService;
        Title = "Library";
    }

    [RelayCommand]
    private async Task LoadFoldersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var savedFolders = GetSavedFolders();
            Folders.Clear();

            foreach (var folder in savedFolders)
            {
                // Get track count from DB
                var tracks = await _databaseService.GetTracksByFolderAsync(folder.Id);
                folder.TrackCount = tracks.Count;
                Folders.Add(folder);
            }

            FolderCount = Folders.Count;
            IsEmpty = Folders.Count == 0;
            EmptyMessage = "No folders added yet. Browse Google Drive to add music folders.";
        }, "Failed to load folders");
    }

    [RelayCommand]
    private async Task OpenFolderAsync(SavedFolder folder)
    {
        await Shell.Current.GoToAsync($"folderbrowser?folderId={folder.Id}&folderName={Uri.EscapeDataString(folder.Name)}&mode=library");
    }

    [RelayCommand]
    private async Task BrowseDriveAsync()
    {
        await Shell.Current.GoToAsync("folderbrowser?mode=browse");
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(SavedFolder folder)
    {
        var action = await Shell.Current.DisplayActionSheet(
            $"\"{folder.Name}\" を削除",
            "Cancel",
            null,
            "リストから削除（キャッシュ保持）",
            "リストとキャッシュを完全削除");

        if (action == null || action == "Cancel") return;

        bool deleteCache = action.Contains("完全削除");

        if (deleteCache)
        {
            // Delete cached files for tracks in this folder
            var tracks = await _databaseService.GetTracksByFolderAsync(folder.Id);
            foreach (var track in tracks)
            {
                try
                {
                    // Delete cache entry and file
                    var cached = await _databaseService.GetCachedFileAsync(track.DriveFileId);
                    if (cached != null)
                    {
                        if (File.Exists(cached.LocalPath))
                            File.Delete(cached.LocalPath);
                        await _databaseService.DeleteCachedFileAsync(track.DriveFileId);
                    }
                }
                catch { /* continue cleanup */ }
            }

            // Delete tracks from DB
            await _databaseService.DeleteTracksByFolderAsync(folder.Id);
        }

        // Remove folder from saved list
        RemoveSavedFolder(folder.Id);
        Folders.Remove(folder);
        FolderCount = Folders.Count;
        IsEmpty = Folders.Count == 0;
    }

    public static List<SavedFolder> GetSavedFolders()
    {
        var json = Preferences.Get("saved_folders_v2", "");
        if (string.IsNullOrEmpty(json))
        {
            // Migrate from old format
            return MigrateOldFolders();
        }

        try
        {
            return JsonSerializer.Deserialize<List<SavedFolder>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void AddSavedFolder(string folderId, string folderName)
    {
        var folders = GetSavedFolders();
        if (folders.Any(f => f.Id == folderId)) return;

        folders.Add(new SavedFolder { Id = folderId, Name = folderName });
        SaveFolders(folders);
    }

    public static void RemoveSavedFolder(string folderId)
    {
        var folders = GetSavedFolders();
        folders.RemoveAll(f => f.Id == folderId);
        SaveFolders(folders);
    }

    private static void SaveFolders(List<SavedFolder> folders)
    {
        var json = JsonSerializer.Serialize(folders);
        Preferences.Set("saved_folders_v2", json);
    }

    private static List<SavedFolder> MigrateOldFolders()
    {
        var oldFolders = Preferences.Get("selected_folders", "");
        if (string.IsNullOrEmpty(oldFolders)) return [];

        var folders = oldFolders.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => new SavedFolder { Id = id, Name = id }) // Name will be resolved later
            .ToList();

        SaveFolders(folders);
        return folders;
    }
}
