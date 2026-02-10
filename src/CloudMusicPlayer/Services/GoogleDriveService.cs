using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace CloudMusicPlayer.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private readonly IGoogleAuthService _authService;
    private DriveService? _cachedService;
    private DateTime _serviceCreatedAt;

    public GoogleDriveService(IGoogleAuthService authService)
    {
        _authService = authService;
    }

    private async Task<DriveService> GetDriveServiceAsync(bool forceNew = false)
    {
        // Reuse cached DriveService if it's less than 45 minutes old
        if (!forceNew && _cachedService != null && (DateTime.UtcNow - _serviceCreatedAt).TotalMinutes < 45)
        {
            return _cachedService;
        }

        var credential = await _authService.GetCurrentCredentialAsync()
            ?? throw new InvalidOperationException("Not signed in to Google");

        _cachedService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CloudMusicPlayer"
        });
        _serviceCreatedAt = DateTime.UtcNow;
        System.Diagnostics.Debug.WriteLine("[Drive] Created new DriveService");
        return _cachedService;
    }

    /// <summary>Invalidate the cached service (e.g. after token refresh).</summary>
    private void InvalidateCachedService()
    {
        _cachedService = null;
    }

    public async Task<List<DriveFileItem>> GetFilesInFolderAsync(string folderId = "root")
    {
        var service = await GetDriveServiceAsync();
        var items = new List<DriveFileItem>();
        string? pageToken = null;

        do
        {
            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "nextPageToken, files(id, name, mimeType, size, modifiedTime, parents)";
            request.OrderBy = "folder,name";
            request.PageSize = 100;
            request.PageToken = pageToken;

            var result = await request.ExecuteAsync();

            if (result.Files != null)
            {
                items.AddRange(result.Files.Select(f => new DriveFileItem
                {
                    Id = f.Id,
                    Name = f.Name,
                    MimeType = f.MimeType,
                    Size = f.Size,
                    ModifiedTime = f.ModifiedTimeDateTimeOffset?.DateTime,
                    ParentId = folderId
                }));
            }

            pageToken = result.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return items;
    }

    public async Task<List<DriveFileItem>> GetAudioFilesInFolderAsync(string folderId, bool recursive = false)
    {
        var allFiles = await GetFilesInFolderAsync(folderId);
        var audioFiles = allFiles.Where(f => f.IsAudioFile).ToList();

        if (recursive)
        {
            var folders = allFiles.Where(f => f.IsFolder).ToList();
            foreach (var folder in folders)
            {
                var subFiles = await GetAudioFilesInFolderAsync(folder.Id, true);
                audioFiles.AddRange(subFiles);
            }
        }

        return audioFiles;
    }

    public async Task<Stream> DownloadFileAsync(string fileId, IProgress<long>? progress = null)
    {
        var service = await GetDriveServiceAsync();
        var request = service.Files.Get(fileId);

        var memoryStream = new MemoryStream();

        if (progress != null)
        {
            request.MediaDownloader.ProgressChanged += p =>
            {
                if (p.BytesDownloaded > 0)
                    progress.Report(p.BytesDownloaded);
            };
        }

        await request.DownloadAsync(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DownloadFileToPathAsync(string fileId, string localPath, IProgress<long>? progress = null)
    {
        // Try download, retry once with fresh credentials on auth failure
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var service = await GetDriveServiceAsync();
            var request = service.Files.Get(fileId);

            if (progress != null)
            {
                request.MediaDownloader.ProgressChanged += p =>
                {
                    if (p.BytesDownloaded > 0)
                        progress.Report(p.BytesDownloaded);
                };
            }

            using var fileStream = File.Create(localPath);
            var downloadProgress = await request.DownloadAsync(fileStream);

            if (downloadProgress.Status == Google.Apis.Download.DownloadStatus.Completed)
            {
                System.Diagnostics.Debug.WriteLine($"[Drive] Downloaded {fileId} ({fileStream.Length} bytes)");
                return;
            }

            var isAuthError = downloadProgress.Exception?.Message?.Contains("Unauthorized") == true
                           || downloadProgress.Exception?.Message?.Contains("401") == true;

            if (isAuthError && attempt == 0)
            {
                System.Diagnostics.Debug.WriteLine("[Drive] Auth error, force-refreshing token and retrying...");
                InvalidateCachedService();
                var refreshed = await _authService.ForceRefreshTokenAsync();
                if (!refreshed)
                    throw new Exception("Authentication expired. Please sign in again.");
                System.Diagnostics.Debug.WriteLine("[Drive] Token force-refreshed, retrying download...");
                continue;
            }

            throw new Exception($"Download failed: {downloadProgress.Exception?.Message}");
        }
    }

    public async Task<DriveFileItem?> GetFileInfoAsync(string fileId)
    {
        try
        {
            var service = await GetDriveServiceAsync();
            var request = service.Files.Get(fileId);
            request.Fields = "id, name, mimeType, size, modifiedTime, parents";
            var file = await request.ExecuteAsync();

            return new DriveFileItem
            {
                Id = file.Id,
                Name = file.Name,
                MimeType = file.MimeType,
                Size = file.Size,
                ModifiedTime = file.ModifiedTimeDateTimeOffset?.DateTime,
                ParentId = file.Parents?.FirstOrDefault()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GetFolderNameAsync(string folderId)
    {
        if (folderId == "root") return "My Drive";

        try
        {
            var service = await GetDriveServiceAsync();
            var request = service.Files.Get(folderId);
            request.Fields = "name";
            var file = await request.ExecuteAsync();
            return file.Name;
        }
        catch
        {
            return "Unknown Folder";
        }
    }

    public async Task<string?> DownloadPartialToTempAsync(string fileId, string fileExtension, long maxBytes = 512 * 1024)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var credential = await _authService.GetCurrentCredentialAsync();
                if (credential?.Token?.AccessToken == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Drive] Partial download {fileId}: no access token");
                    return null;
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.Token.AccessToken);

                var url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, maxBytes - 1);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Retry on auth error with refreshed token
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && attempt == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[Drive] Partial download {fileId}: 401, refreshing token...");
                    InvalidateCachedService();
                    var refreshed = await _authService.ForceRefreshTokenAsync();
                    if (refreshed) continue;
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var tempPath = Path.Combine(Path.GetTempPath(), $"cmp_meta_{fileId}{fileExtension}");
                using (var fs = File.Create(tempPath))
                {
                    await response.Content.CopyToAsync(fs);
                }

                System.Diagnostics.Debug.WriteLine($"[Drive] Partial download {fileId}: {new FileInfo(tempPath).Length} bytes");
                return tempPath;
            }
            catch (HttpRequestException ex) when (attempt == 0 && ex.Message.Contains("401"))
            {
                System.Diagnostics.Debug.WriteLine($"[Drive] Partial download {fileId}: auth exception, refreshing token...");
                InvalidateCachedService();
                var refreshed = await _authService.ForceRefreshTokenAsync();
                if (!refreshed) return null;
                continue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Drive] Partial download failed for {fileId}: {ex.Message}");
                return null;
            }
        }

        return null;
    }
}
