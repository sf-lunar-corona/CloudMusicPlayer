#if ANDROID
using Android.Media.Audiofx;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Platforms.Android.Services;

public class AndroidEqualizerService : IEqualizerService
{
    private Equalizer? _equalizer;
    private bool _isInitialized;

    public bool IsSupported => true;
    public int NumberOfBands => _equalizer?.NumberOfBands ?? 5;

    public int[] BandFrequencies
    {
        get
        {
            if (_equalizer == null) return [60, 230, 910, 4000, 14000];

            var freqs = new int[NumberOfBands];
            for (short i = 0; i < NumberOfBands; i++)
            {
                var range = _equalizer.GetCenterFreq(i);
                freqs[i] = range / 1000; // Convert from milliHz to Hz
            }
            return freqs;
        }
    }

    public int MinLevel => _equalizer != null ? GetBandRange()[0] / 100 : -15;
    public int MaxLevel => _equalizer != null ? GetBandRange()[1] / 100 : 15;

    private short[] GetBandRange()
    {
        try
        {
            var range = _equalizer!.GetBandLevelRange();
            if (range != null && range.Length >= 2)
                return range;
        }
        catch { }
        return [-1500, 1500];
    }
    public bool IsEnabled { get; private set; }

    public Task<bool> InitializeAsync(int audioSessionId = 0)
    {
        try
        {
            if (_isInitialized)
            {
                Release();
            }

            _equalizer = new Equalizer(0, audioSessionId);
            _isInitialized = true;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize equalizer: {ex}");
            return Task.FromResult(false);
        }
    }

    public void SetBandLevel(int band, int level)
    {
        if (_equalizer == null || band >= NumberOfBands) return;
        _equalizer.SetBandLevel((short)band, (short)(level * 100));
    }

    public int GetBandLevel(int band)
    {
        if (_equalizer == null || band >= NumberOfBands) return 0;
        return _equalizer.GetBandLevel((short)band) / 100;
    }

    public void ApplyPreset(EqualizerPreset preset)
    {
        if (_equalizer == null) return;

        for (int i = 0; i < Math.Min(preset.BandLevels.Length, NumberOfBands); i++)
        {
            SetBandLevel(i, preset.BandLevels[i]);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (_equalizer != null)
        {
            _equalizer.SetEnabled(enabled);
            IsEnabled = enabled;
        }
    }

    public void Release()
    {
        _equalizer?.Release();
        _equalizer = null;
        _isInitialized = false;
    }
}
#endif
