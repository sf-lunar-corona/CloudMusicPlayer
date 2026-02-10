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
    private readonly IGoogleDriveService _driveService;
    private readonly IGoogleAuthService _authService;

    public ObservableCollection<SavedFolder> Folders { get; } = [];

    [ObservableProperty]
    private int _folderCount;

    [ObservableProperty]
    private bool _isSyncing;

    public LibraryViewModel(
        IDatabaseService databaseService,
        IAudioPlaybackService playbackService,
        ICacheService cacheService,
        IGoogleDriveService driveService,
        IGoogleAuthService authService)
    {
        _databaseService = databaseService;
        _playbackService = playbackService;
        _cacheService = cacheService;
        _driveService = driveService;
        _authService = authService;
        Title = "Library";
    }

    [RelayCommand]
    private async Task LoadFoldersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var savedFolders = GetSavedFolders();
            System.Diagnostics.Debug.WriteLine($"[Library] LoadFolders: {savedFolders.Count} saved folders");
            Folders.Clear();

            foreach (var folder in savedFolders)
            {
                // Get track count from DB
                var tracks = await _databaseService.GetTracksByFolderAsync(folder.Id);
                folder.TrackCount = tracks.Count;
                System.Diagnostics.Debug.WriteLine($"[Library]   Folder \"{folder.Name}\" (ID={folder.Id}): {tracks.Count} tracks in DB");
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
    private async Task SyncFoldersAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;

        try
        {
            var savedFolders = GetSavedFolders();
            var totalAdded = 0;

            foreach (var folder in savedFolders)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Library] Sync: scanning folder \"{folder.Name}\" (ID={folder.Id})...");
                    var driveFiles = await _driveService.GetAudioFilesInFolderAsync(folder.Id, recursive: true);
                    System.Diagnostics.Debug.WriteLine($"[Library] Sync: found {driveFiles.Count} audio files in \"{folder.Name}\"");

                    var newInFolder = 0;
                    foreach (var file in driveFiles)
                    {
                        var existing = await _databaseService.GetTrackByDriveIdAsync(file.Id);
                        if (existing != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Library] Sync:   SKIP (exists) {file.Name} FolderId={existing.FolderId}");
                            continue;
                        }

                        var track = new AudioTrack
                        {
                            DriveFileId = file.Id,
                            Title = Path.GetFileNameWithoutExtension(file.Name),
                            FileName = file.Name,
                            FileExtension = Path.GetExtension(file.Name).ToLowerInvariant(),
                            FileSize = file.Size ?? 0,
                            MimeType = file.MimeType,
                            FolderId = folder.Id,
                            FolderName = folder.Name,
                            DateAdded = DateTime.UtcNow
                        };
                        await _databaseService.SaveTrackAsync(track);
                        totalAdded++;
                        newInFolder++;
                    }
                    System.Diagnostics.Debug.WriteLine($"[Library] Sync: {newInFolder} new tracks saved for \"{folder.Name}\"");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Library] Sync FAILED for \"{folder.Name}\": {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Refresh folder track counts
            foreach (var folder in Folders)
            {
                var tracks = await _databaseService.GetTracksByFolderAsync(folder.Id);
                folder.TrackCount = tracks.Count;
            }

            // Force UI refresh
            var currentFolders = Folders.ToList();
            Folders.Clear();
            foreach (var f in currentFolders)
                Folders.Add(f);

            if (totalAdded > 0)
                await Shell.Current.DisplayAlert("Sync Complete", $"{totalAdded} new tracks found.", "OK");
            else
                await Shell.Current.DisplayAlert("Sync Complete", "No new tracks found.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Sync Failed", ex.Message, "OK");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDriveAsync()
    {
        var isSignedIn = await _authService.IsSignedInAsync();
        if (!isSignedIn)
        {
            var signIn = await Shell.Current.DisplayAlert(
                "Sign In Required",
                "You need to sign in to Google to browse Drive folders.",
                "Sign In",
                "Cancel");

            if (!signIn) return;

            var credential = await _authService.SignInAsync();
            if (credential == null)
            {
                await Shell.Current.DisplayAlert("Sign In Failed", "Could not sign in to Google. Please try again.", "OK");
                return;
            }
        }

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
