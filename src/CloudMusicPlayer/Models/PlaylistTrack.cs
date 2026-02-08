using SQLite;

namespace CloudMusicPlayer.Models;

public class PlaylistTrack
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PlaylistId { get; set; }

    [Indexed]
    public int TrackId { get; set; }

    public int SortOrder { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
