namespace CloudMusicPlayer.Models;

public class EqualizerPreset
{
    public string Name { get; set; } = string.Empty;
    public int[] BandLevels { get; set; } = [];
    public bool IsCustom { get; set; }

    public static List<EqualizerPreset> GetDefaultPresets() =>
    [
        new() { Name = "Flat", BandLevels = [0, 0, 0, 0, 0] },
        new() { Name = "Rock", BandLevels = [4, 2, 0, 2, 4] },
        new() { Name = "Pop", BandLevels = [-1, 3, 5, 3, -1] },
        new() { Name = "Jazz", BandLevels = [3, 1, 0, 1, 3] },
        new() { Name = "Classical", BandLevels = [4, 2, 0, 2, 4] },
        new() { Name = "Bass Boost", BandLevels = [5, 3, 0, 0, 0] },
        new() { Name = "Treble Boost", BandLevels = [0, 0, 0, 3, 5] },
        new() { Name = "Vocal", BandLevels = [-2, 0, 4, 2, -1] },
    ];
}
