using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface ICacheService
{
    Task<string?> GetCachedFilePathAsync(string driveFileId);
    Task<string> DownloadAndCacheAsync(AudioTrack track, IProgress<double>? progress = null);
    Task<long> GetCacheSizeAsync();
    Task ClearCacheAsync();
    Task EvictOldFilesAsync(long targetSize);
    Task<bool> IsFileCachedAsync(string driveFileId);
}
