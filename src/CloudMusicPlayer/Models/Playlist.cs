using SQLite;

namespace CloudMusicPlayer.Models;

public class Playlist
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public int TrackCount { get; set; }

    [Ignore]
    public List<AudioTrack> Tracks { get; set; } = [];
}
