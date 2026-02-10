using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IGoogleDriveService
{
    Task<List<DriveFileItem>> GetFilesInFolderAsync(string folderId = "root");
    Task<List<DriveFileItem>> GetAudioFilesInFolderAsync(string folderId, bool recursive = false);
    Task<Stream> DownloadFileAsync(string fileId, IProgress<long>? progress = null);
    Task DownloadFileToPathAsync(string fileId, string localPath, IProgress<long>? progress = null);
    Task<DriveFileItem?> GetFileInfoAsync(string fileId);
    Task<string> GetFolderNameAsync(string folderId);
    Task<string?> DownloadPartialToTempAsync(string fileId, string fileExtension, long maxBytes = 512 * 1024);
}
