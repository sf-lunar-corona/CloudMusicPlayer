using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class EqualizerViewModel : BaseViewModel
{
    private readonly IEqualizerService _equalizerService;

    public ObservableCollection<EqualizerPreset> Presets { get; } = [];

    [ObservableProperty]
    private EqualizerPreset? _selectedPreset;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isSupported;

    [ObservableProperty]
    private string _notSupportedMessage = string.Empty;

    [ObservableProperty]
    private int _band1;

    [ObservableProperty]
    private int _band2;

    [ObservableProperty]
    private int _band3;

    [ObservableProperty]
    private int _band4;

    [ObservableProperty]
    private int _band5;

    [ObservableProperty]
    private int _minLevel;

    [ObservableProperty]
    private int _maxLevel;

    public EqualizerViewModel(IEqualizerService equalizerService)
    {
        _equalizerService = equalizerService;
        Title = "Equalizer";
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsSupported = _equalizerService.IsSupported;

        if (!IsSupported)
        {
#if IOS
            NotSupportedMessage = "Equalizer is not available on iOS in this version. Coming soon!";
#elif WINDOWS
            NotSupportedMessage = "Equalizer support is limited on Windows. Coming soon!";
#else
            NotSupportedMessage = "Equalizer is not available on this platform.";
#endif
            return;
        }

        await _equalizerService.InitializeAsync();

        MinLevel = _equalizerService.MinLevel;
        MaxLevel = _equalizerService.MaxLevel;
        IsEnabled = _equalizerService.IsEnabled;

        var presets = EqualizerPreset.GetDefaultPresets();
        Presets.Clear();
        foreach (var preset in presets)
            Presets.Add(preset);

        LoadCurrentLevels();
    }

    [RelayCommand]
    private void ApplyPreset(EqualizerPreset preset)
    {
        SelectedPreset = preset;
        _equalizerService.ApplyPreset(preset);
        LoadCurrentLevels();
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        IsEnabled = !IsEnabled;
        _equalizerService.SetEnabled(IsEnabled);
    }

    partial void OnBand1Changed(int value) => SetBandLevel(0, value);
    partial void OnBand2Changed(int value) => SetBandLevel(1, value);
    partial void OnBand3Changed(int value) => SetBandLevel(2, value);
    partial void OnBand4Changed(int value) => SetBandLevel(3, value);
    partial void OnBand5Changed(int value) => SetBandLevel(4, value);

    private void SetBandLevel(int band, int level)
    {
        if (IsSupported && _equalizerService.NumberOfBands > band)
        {
            _equalizerService.SetBandLevel(band, level);
            SelectedPreset = null; // Custom setting
        }
    }

    private void LoadCurrentLevels()
    {
        if (!IsSupported) return;

        var bands = _equalizerService.NumberOfBands;
        if (bands > 0) Band1 = _equalizerService.GetBandLevel(0);
        if (bands > 1) Band2 = _equalizerService.GetBandLevel(1);
        if (bands > 2) Band3 = _equalizerService.GetBandLevel(2);
        if (bands > 3) Band4 = _equalizerService.GetBandLevel(3);
        if (bands > 4) Band5 = _equalizerService.GetBandLevel(4);
    }
}
