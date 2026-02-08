using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public enum RepeatMode
{
    None,
    All,
    One
}

public interface IAudioPlaybackService
{
    AudioTrack? CurrentTrack { get; }
    bool IsPlaying { get; }
    bool IsShuffleEnabled { get; set; }
    RepeatMode RepeatMode { get; set; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    List<AudioTrack> Queue { get; }
    int CurrentIndex { get; }

    bool IsLoading { get; }

    event EventHandler<AudioTrack?>? TrackChanged;
    event EventHandler<bool>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<bool>? LoadingStateChanged;

    Task PlayAsync(AudioTrack track);
    Task PlayAsync(List<AudioTrack> tracks, int startIndex = 0);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekAsync(TimeSpan position);
    void AddToQueue(AudioTrack track);
    void RemoveFromQueue(int index);
    void ClearQueue();
    void SetQueue(List<AudioTrack> tracks, int startIndex = 0);
}
