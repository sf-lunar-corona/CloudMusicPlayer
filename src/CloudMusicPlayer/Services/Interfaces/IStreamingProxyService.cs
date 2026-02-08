namespace CloudMusicPlayer.Services.Interfaces;

public interface IStreamingProxyService
{
    /// <summary>Start the local proxy server.</summary>
    void Start();

    /// <summary>Get a local streaming URL for a Google Drive file.</summary>
    string GetStreamUrl(string fileId, string extension);

    /// <summary>Whether the proxy server is running.</summary>
    bool IsRunning { get; }
}
