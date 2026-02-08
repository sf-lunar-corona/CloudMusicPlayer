namespace CloudMusicPlayer.Models;

public class SavedFolder
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}
