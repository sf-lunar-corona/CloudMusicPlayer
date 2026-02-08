using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class StreamingProxyService : IStreamingProxyService, IDisposable
{
    private readonly IGoogleAuthService _authService;
    private readonly IDatabaseService _databaseService;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _port;

    public bool IsRunning => _listener?.IsListening == true;

    public StreamingProxyService(IGoogleAuthService authService, IDatabaseService databaseService)
    {
        _authService = authService;
        _databaseService = databaseService;
    }

    public void Start()
    {
        if (_listener != null) return;

        // Find an available port
        using (var tempSocket = new TcpListener(IPAddress.Loopback, 0))
        {
            tempSocket.Start();
            _port = ((IPEndPoint)tempSocket.LocalEndpoint).Port;
            tempSocket.Stop();
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoopAsync(_cts.Token));

        System.Diagnostics.Debug.WriteLine($"[Proxy] Started on port {_port}");
    }

    public string GetStreamUrl(string fileId, string extension)
    {
        return $"http://127.0.0.1:{_port}/{fileId}{extension}";
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Proxy] Listen error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath?.TrimStart('/') ?? "";
        var fileId = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        System.Diagnostics.Debug.WriteLine($"[Proxy] Request: {path} (Range: {context.Request.Headers["Range"]})");

        if (string.IsNullOrEmpty(fileId))
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        try
        {
            // Check if file is already cached on disk
            var cachedFile = await _databaseService.GetCachedFileAsync(fileId);
            if (cachedFile != null && File.Exists(cachedFile.LocalPath))
            {
                System.Diagnostics.Debug.WriteLine($"[Proxy] Serving from cache: {cachedFile.LocalPath}");
                await ServeFromFileAsync(context, cachedFile.LocalPath);
                return;
            }

            // Stream from Google Drive and save to cache simultaneously
            await StreamFromDriveAsync(context, fileId, extension);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Proxy] Error handling {fileId}: {ex.Message}");
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static async Task ServeFromFileAsync(HttpListenerContext context, string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var rangeHeader = context.Request.Headers["Range"];

        using var fs = File.OpenRead(filePath);

        if (!string.IsNullOrEmpty(rangeHeader) && TryParseRange(rangeHeader, fileInfo.Length, out var start, out var end))
        {
            // Partial content
            var length = end - start + 1;
            context.Response.StatusCode = 206;
            context.Response.ContentLength64 = length;
            context.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
            context.Response.Headers.Add("Accept-Ranges", "bytes");

            fs.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[81920];
            long remaining = length;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await fs.ReadAsync(buffer.AsMemory(0, toRead));
                if (read == 0) break;
                await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
        }
        else
        {
            // Full content
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = fileInfo.Length;
            context.Response.Headers.Add("Accept-Ranges", "bytes");
            await fs.CopyToAsync(context.Response.OutputStream);
        }
    }

    private async Task StreamFromDriveAsync(HttpListenerContext context, string fileId, string extension)
    {
        var credential = await _authService.GetCurrentCredentialAsync();
        if (credential == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var driveUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
        var request = new HttpRequestMessage(HttpMethod.Get, driveUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token.AccessToken);

        // Forward Range header for seeking
        var rangeHeader = context.Request.Headers["Range"];
        bool isRangeRequest = !string.IsNullOrEmpty(rangeHeader);
        if (isRangeRequest)
        {
            request.Headers.TryAddWithoutValidation("Range", rangeHeader);
        }

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            System.Diagnostics.Debug.WriteLine($"[Proxy] Drive returned {response.StatusCode}");
            context.Response.StatusCode = (int)response.StatusCode;
            return;
        }

        // Set response headers
        context.Response.StatusCode = (int)response.StatusCode;
        if (response.Content.Headers.ContentLength.HasValue)
            context.Response.ContentLength64 = response.Content.Headers.ContentLength.Value;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        context.Response.Headers.Add("Accept-Ranges", "bytes");

        if (response.Content.Headers.ContentRange != null)
            context.Response.Headers.Add("Content-Range", response.Content.Headers.ContentRange.ToString());

        // Stream to client, and also write to cache file if this is a full (non-range) request
        using var sourceStream = await response.Content.ReadAsStreamAsync();
        var cachePath = Path.Combine(Constants.CacheDirectory, $"{fileId}{extension}");
        FileStream? cacheStream = null;

        if (!isRangeRequest)
        {
            try
            {
                if (!Directory.Exists(Constants.CacheDirectory))
                    Directory.CreateDirectory(Constants.CacheDirectory);
                cacheStream = File.Create(cachePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Proxy] Can't create cache file: {ex.Message}");
            }
        }

        try
        {
            var buffer = new byte[81920]; // 80KB chunks
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory())) > 0)
            {
                await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                cacheStream?.Write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }

            System.Diagnostics.Debug.WriteLine($"[Proxy] Streamed {totalBytes} bytes for {fileId}");

            // Register in cache DB if we wrote a complete file
            if (cacheStream != null && totalBytes > 0)
            {
                cacheStream.Dispose();
                cacheStream = null;

                var cachedFile = new CachedFile
                {
                    DriveFileId = fileId,
                    LocalPath = cachePath,
                    FileSize = totalBytes,
                    CachedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow
                };
                await _databaseService.SaveCachedFileAsync(cachedFile);
                System.Diagnostics.Debug.WriteLine($"[Proxy] Cached {fileId} ({totalBytes} bytes)");
            }
        }
        finally
        {
            cacheStream?.Dispose();
        }
    }

    private static bool TryParseRange(string rangeHeader, long fileSize, out long start, out long end)
    {
        start = 0;
        end = fileSize - 1;

        // Parse "bytes=START-END" or "bytes=START-"
        if (!rangeHeader.StartsWith("bytes=")) return false;

        var range = rangeHeader["bytes=".Length..];
        var parts = range.Split('-');
        if (parts.Length != 2) return false;

        if (!string.IsNullOrEmpty(parts[0]))
            start = long.Parse(parts[0]);

        if (!string.IsNullOrEmpty(parts[1]))
            end = long.Parse(parts[1]);
        else
            end = fileSize - 1;

        return start >= 0 && start <= end && end < fileSize;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
    }
}
