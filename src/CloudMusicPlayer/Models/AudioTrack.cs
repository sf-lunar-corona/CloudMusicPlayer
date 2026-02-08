using SQLite;

namespace CloudMusicPlayer.Models;

public class AudioTrack
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string DriveFileId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = "Unknown Artist";
    public string Album { get; set; } = "Unknown Album";
    public int TrackNumber { get; set; }
    public TimeSpan Duration { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string? CachedFilePath { get; set; }
    public string? AlbumArtPath { get; set; }
    public string FolderId { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayed { get; set; }
    public int PlayCount { get; set; }

    [Ignore]
    public bool IsCached => !string.IsNullOrEmpty(CachedFilePath) && File.Exists(CachedFilePath);

    [Ignore]
    public string DisplayDuration => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");

    [Ignore]
    public string DisplayInfo => $"{Artist} • {Album}";
}
