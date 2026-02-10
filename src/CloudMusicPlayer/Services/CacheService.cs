using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class CacheService : ICacheService
{
    private readonly IGoogleDriveService _driveService;
    private readonly IDatabaseService _databaseService;
    private readonly IMetadataService _metadataService;
    private readonly SemaphoreSlim _downloadLock = new(3); // Max 3 concurrent downloads

    public CacheService(IGoogleDriveService driveService, IDatabaseService databaseService, IMetadataService metadataService)
    {
        _driveService = driveService;
        _databaseService = databaseService;
        _metadataService = metadataService;

        if (!Directory.Exists(Constants.CacheDirectory))
            Directory.CreateDirectory(Constants.CacheDirectory);
    }

    public async Task<string?> GetCachedFilePathAsync(string driveFileId)
    {
        var cached = await _databaseService.GetCachedFileAsync(driveFileId);
        if (cached == null) return null;

        if (File.Exists(cached.LocalPath))
        {
            // Defer LastAccessed update — don't block the hot path
            _ = Task.Run(async () =>
            {
                cached.LastAccessed = DateTime.UtcNow;
                await _databaseService.SaveCachedFileAsync(cached);
            });
            return cached.LocalPath;
        }

        // File missing from disk, remove DB entry
        await _databaseService.DeleteCachedFileAsync(driveFileId);
        return null;
    }

    public async Task<string> DownloadAndCacheAsync(AudioTrack track, IProgress<double>? progress = null)
    {
        // Check if already cached
        var existing = await GetCachedFilePathAsync(track.DriveFileId);
        if (existing != null) return existing;

        await _downloadLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            existing = await GetCachedFilePathAsync(track.DriveFileId);
            if (existing != null) return existing;

            // Ensure we have space
            await EnsureCacheSpaceAsync(track.FileSize);

            var extension = Path.GetExtension(track.FileName);
            var localPath = Path.Combine(Constants.CacheDirectory, $"{track.DriveFileId}{extension}");

            var downloadProgress = progress != null
                ? new Progress<long>(bytes =>
                {
                    if (track.FileSize > 0)
                        progress.Report((double)bytes / track.FileSize * 100);
                })
                : null;

            await _driveService.DownloadFileToPathAsync(track.DriveFileId, localPath, downloadProgress);

            var fileInfo = new FileInfo(localPath);
            var cachedFile = new CachedFile
            {
                DriveFileId = track.DriveFileId,
                LocalPath = localPath,
                FileSize = fileInfo.Length,
                CachedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };

            await _databaseService.SaveCachedFileAsync(cachedFile);

            // Update track with cached path
            track.CachedFilePath = localPath;

            // Extract metadata if not yet populated
            if (track.Artist == "Unknown Artist" || track.Album == "Unknown Album" || track.Duration == TimeSpan.Zero)
            {
                try
                {
                    await _metadataService.ExtractMetadataAsync(localPath, track);
                    if (string.IsNullOrEmpty(track.AlbumArtPath))
                        track.AlbumArtPath = await _metadataService.ExtractAlbumArtAsync(localPath, track.DriveFileId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Cache] Metadata extraction failed (non-fatal): {ex.Message}");
                }
            }

            await _databaseService.SaveTrackAsync(track);

            return localPath;
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    public async Task<long> GetCacheSizeAsync()
    {
        return await _databaseService.GetTotalCacheSizeAsync();
    }

    public async Task ClearCacheAsync()
    {
        var files = await _databaseService.GetAllCachedFilesAsync();
        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file.LocalPath))
                    File.Delete(file.LocalPath);
            }
            catch { /* ignore individual file deletion errors */ }
        }
        await _databaseService.ClearCachedFilesAsync();
    }

    public async Task EvictOldFilesAsync(long targetSize)
    {
        var currentSize = await GetCacheSizeAsync();
        if (currentSize <= targetSize) return;

        var files = await _databaseService.GetAllCachedFilesAsync(); // sorted by LastAccessed ASC

        foreach (var file in files)
        {
            if (currentSize <= targetSize) break;

            try
            {
                if (File.Exists(file.LocalPath))
                    File.Delete(file.LocalPath);

                currentSize -= file.FileSize;
                await _databaseService.DeleteCachedFileAsync(file.DriveFileId);
            }
            catch { /* continue evicting */ }
        }
    }

    public async Task<bool> IsFileCachedAsync(string driveFileId)
    {
        var path = await GetCachedFilePathAsync(driveFileId);
        return path != null;
    }

    private async Task EnsureCacheSpaceAsync(long requiredBytes)
    {
        var currentSize = await GetCacheSizeAsync();
        var limit = Preferences.Get("cache_size_limit", Constants.DefaultCacheSizeLimitBytes);

        if (currentSize + requiredBytes > limit)
        {
            await EvictOldFilesAsync(limit - requiredBytes);
        }
    }
}
