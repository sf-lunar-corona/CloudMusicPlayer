using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IMetadataService
{
    Task<AudioTrack> ExtractMetadataAsync(string filePath, AudioTrack track);
    Task<string?> ExtractAlbumArtAsync(string filePath, string driveFileId);
}
