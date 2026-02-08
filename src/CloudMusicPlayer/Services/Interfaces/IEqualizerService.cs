using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IEqualizerService
{
    bool IsSupported { get; }
    int NumberOfBands { get; }
    int[] BandFrequencies { get; }
    int MinLevel { get; }
    int MaxLevel { get; }

    Task<bool> InitializeAsync(int audioSessionId = 0);
    void SetBandLevel(int band, int level);
    int GetBandLevel(int band);
    void ApplyPreset(EqualizerPreset preset);
    void SetEnabled(bool enabled);
    bool IsEnabled { get; }
    void Release();
}
