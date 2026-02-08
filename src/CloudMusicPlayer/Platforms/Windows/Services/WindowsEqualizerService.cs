#if WINDOWS
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Platforms.Windows.Services;

public class WindowsEqualizerService : IEqualizerService
{
    public bool IsSupported => false;
    public int NumberOfBands => 0;
    public int[] BandFrequencies => [];
    public int MinLevel => -15;
    public int MaxLevel => 15;
    public bool IsEnabled => false;

    public Task<bool> InitializeAsync(int audioSessionId = 0) => Task.FromResult(false);
    public void SetBandLevel(int band, int level) { }
    public int GetBandLevel(int band) => 0;
    public void ApplyPreset(EqualizerPreset preset) { }
    public void SetEnabled(bool enabled) { }
    public void Release() { }
}
#endif
