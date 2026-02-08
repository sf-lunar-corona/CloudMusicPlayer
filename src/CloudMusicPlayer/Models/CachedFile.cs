using SQLite;

namespace CloudMusicPlayer.Models;

public class CachedFile
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string DriveFileId { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
}
