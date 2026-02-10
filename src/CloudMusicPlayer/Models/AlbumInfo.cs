namespace CloudMusicPlayer.Models;

public class AlbumInfo
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = "Unknown Artist";
    public int TrackCount { get; set; }
    public string? AlbumArtPath { get; set; }
}
