namespace CloudMusicPlayer.Models;

public class DriveFileItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long? Size { get; set; }
    public DateTime? ModifiedTime { get; set; }
    public string? ParentId { get; set; }

    public bool IsFolder => MimeType == "application/vnd.google-apps.folder";
    public bool IsAudioFile => !IsFolder && Constants.SupportedAudioExtensions
        .Any(ext => Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public string Icon => IsFolder ? "📁" : "🎵";
    public string DisplaySize => Size.HasValue ? FormatSize(Size.Value) : string.Empty;

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.#} {sizes[order]}";
    }
}
