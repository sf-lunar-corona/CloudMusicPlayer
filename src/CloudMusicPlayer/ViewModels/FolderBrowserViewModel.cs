using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class FolderBrowserViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IGoogleDriveService _driveService;
    private readonly IDatabaseService _databaseService;
    private readonly IAudioPlaybackService _playbackService;

    public ObservableCollection<DriveFileItem> Items { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];

    [ObservableProperty]
    private string _currentFolderId = "root";

    [ObservableProperty]
    private bool _isBrowseMode = true; // true = "add folder" mode, false = "library" mode

    private string? _initialFolderName;

    public FolderBrowserViewModel(IGoogleDriveService driveService, IDatabaseService databaseService, IAudioPlaybackService playbackService)
    {
        _driveService = driveService;
        _databaseService = databaseService;
        _playbackService = playbackService;
        Title = "Browse Drive";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("folderId", out var folderId) && folderId is string fid && !string.IsNullOrEmpty(fid))
        {
            CurrentFolderId = fid;
        }

        if (query.TryGetValue("folderName", out var folderName) && folderName is string fname)
        {
            _initialFolderName = Uri.UnescapeDataString(fname);
        }

        if (query.TryGetValue("mode", out var mode) && mode is string modeStr)
        {
            IsBrowseMode = modeStr != "library";
            Title = IsBrowseMode ? "Browse Drive" : _initialFolderName ?? "Library";
        }
    }

    [RelayCommand]
    private async Task LoadFolderAsync(string? folderId = null)
    {
        await ExecuteAsync(async () =>
        {
            folderId ??= CurrentFolderId;
            CurrentFolderId = folderId;

            var items = await _driveService.GetFilesInFolderAsync(folderId);

            // Filter to show only folders and audio files
            var filtered = items
                .Where(i => i.IsFolder || i.IsAudioFile)
                .ToList();

            Items.Clear();
            foreach (var item in filtered)
                Items.Add(item);

            IsEmpty = Items.Count == 0;
            EmptyMessage = "No folders or music files found";

            await UpdateBreadcrumbsAsync(folderId);
        }, "Failed to load folder");
    }

    [RelayCommand]
    private async Task NavigateToItemAsync(DriveFileItem item)
    {
        if (item.IsFolder)
        {
            await LoadFolderAsync(item.Id);
        }
        else if (item.IsAudioFile)
        {
            await PlayAudioFileAsync(item);
        }
    }

    private async Task PlayAudioFileAsync(DriveFileItem item)
    {
        await ExecuteAsync(async () =>
        {
            // Save all audio files in current folder to DB and build queue
            var audioItems = Items.Where(i => i.IsAudioFile).ToList();
            var folderName = Breadcrumbs.Count > 0 ? Breadcrumbs[^1].Name : "Unknown";
            var tracks = new List<AudioTrack>();
            int startIndex = 0;

            for (int i = 0; i < audioItems.Count; i++)
            {
                var audioItem = audioItems[i];
                var track = await _databaseService.GetTrackByDriveIdAsync(audioItem.Id);
                if (track == null)
                {
                    track = new AudioTrack
                    {
                        DriveFileId = audioItem.Id,
                        Title = Path.GetFileNameWithoutExtension(audioItem.Name),
                        FileName = audioItem.Name,
                        FileExtension = Path.GetExtension(audioItem.Name).ToLowerInvariant(),
                        FileSize = audioItem.Size ?? 0,
                        MimeType = audioItem.MimeType,
                        FolderId = CurrentFolderId,
                        FolderName = folderName,
                        DateAdded = DateTime.UtcNow
                    };
                    await _databaseService.SaveTrackAsync(track);
                }
                tracks.Add(track);
                if (audioItem.Id == item.Id) startIndex = i;
            }

            // Navigate immediately so user sees NowPlaying page right away
            _playbackService.SetQueue(tracks, startIndex);
            await Shell.Current.GoToAsync("nowplaying");
            // Playback starts (download happens in background, UI updates via events)
            await _playbackService.PlayAsync(tracks, startIndex);
        }, "Failed to play track");
    }

    [RelayCommand]
    private async Task SelectFolderAsync()
    {
        await ExecuteAsync(async () =>
        {
            IsBusy = true;

            // Get all audio files in the current folder recursively
            var audioFiles = await _driveService.GetAudioFilesInFolderAsync(CurrentFolderId, true);
            var folderName = await _driveService.GetFolderNameAsync(CurrentFolderId);

            foreach (var file in audioFiles)
            {
                var existing = await _databaseService.GetTrackByDriveIdAsync(file.Id);
                if (existing == null)
                {
                    var track = new AudioTrack
                    {
                        DriveFileId = file.Id,
                        Title = Path.GetFileNameWithoutExtension(file.Name),
                        FileName = file.Name,
                        FileExtension = Path.GetExtension(file.Name).ToLowerInvariant(),
                        FileSize = file.Size ?? 0,
                        MimeType = file.MimeType,
                        FolderId = CurrentFolderId,
                        FolderName = folderName,
                        DateAdded = DateTime.UtcNow
                    };
                    await _databaseService.SaveTrackAsync(track);
                }
            }

            // Save to library
            LibraryViewModel.AddSavedFolder(CurrentFolderId, folderName);

            await Shell.Current.GoToAsync("//library");
        }, "Failed to add folder");
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        if (Breadcrumbs.Count > 1)
        {
            var parent = Breadcrumbs[^2];
            await LoadFolderAsync(parent.FolderId);
        }
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbItem breadcrumb)
    {
        await LoadFolderAsync(breadcrumb.FolderId);
    }

    private async Task UpdateBreadcrumbsAsync(string folderId)
    {
        if (folderId == "root")
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem { Name = "My Drive", FolderId = "root" });
            return;
        }

        // Use initial folder name if this is the first load and name was passed via query
        string folderName;
        if (_initialFolderName != null && Breadcrumbs.Count == 0)
        {
            folderName = _initialFolderName;
            _initialFolderName = null; // Only use once
        }
        else
        {
            folderName = await _driveService.GetFolderNameAsync(folderId);
        }

        // Check if we're navigating back via breadcrumb
        var existingIndex = -1;
        for (int i = 0; i < Breadcrumbs.Count; i++)
        {
            if (Breadcrumbs[i].FolderId == folderId)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            while (Breadcrumbs.Count > existingIndex + 1)
                Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }
        else
        {
            // In library mode, the root breadcrumb is the folder itself
            if (Breadcrumbs.Count == 0 && !IsBrowseMode)
            {
                Breadcrumbs.Add(new BreadcrumbItem { Name = folderName, FolderId = folderId });
            }
            else
            {
                if (Breadcrumbs.Count == 0)
                    Breadcrumbs.Add(new BreadcrumbItem { Name = "My Drive", FolderId = "root" });

                Breadcrumbs.Add(new BreadcrumbItem { Name = folderName, FolderId = folderId });
            }
        }
    }
}

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
}
