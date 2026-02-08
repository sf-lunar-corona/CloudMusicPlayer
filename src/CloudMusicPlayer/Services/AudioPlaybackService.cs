using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace CloudMusicPlayer.Services;

public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly ICacheService _cacheService;
    private readonly IMetadataService _metadataService;
    private readonly IDatabaseService _databaseService;
    private readonly IStreamingProxyService _streamingProxy;
    private MediaElement? _mediaElement;
    private List<AudioTrack> _originalQueue = [];
    private List<AudioTrack> _shuffledQueue = [];
    private System.Timers.Timer? _positionTimer;
    private bool _isLoadingTrack;

    public AudioTrack? CurrentTrack { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsShuffleEnabled { get; set; }
    public RepeatMode RepeatMode { get; set; }
    public TimeSpan Position => _mediaElement?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _mediaElement?.Duration ?? TimeSpan.Zero;

    public double Volume
    {
        get => _mediaElement?.Volume ?? 1.0;
        set
        {
            if (_mediaElement != null)
                _mediaElement.Volume = Math.Clamp(value, 0, 1);
        }
    }

    public List<AudioTrack> Queue => IsShuffleEnabled ? _shuffledQueue : _originalQueue;
    public int CurrentIndex { get; private set; } = -1;

    public event EventHandler<AudioTrack?>? TrackChanged;
    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<bool>? LoadingStateChanged;

    public AudioPlaybackService(ICacheService cacheService, IMetadataService metadataService, IDatabaseService databaseService, IStreamingProxyService streamingProxy)
    {
        _cacheService = cacheService;
        _metadataService = metadataService;
        _databaseService = databaseService;
        _streamingProxy = streamingProxy;

        // Start the streaming proxy server
        _streamingProxy.Start();

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += (s, e) =>
        {
            if (IsPlaying && _mediaElement != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PositionChanged?.Invoke(this, Position);
                });
            }
        };
    }

    public void SetMediaElement(MediaElement mediaElement)
    {
        if (_mediaElement != null)
        {
            _mediaElement.MediaEnded -= OnMediaEnded;
            _mediaElement.MediaFailed -= OnMediaFailed;
            _mediaElement.StateChanged -= OnStateChanged;
        }

        _mediaElement = mediaElement;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.StateChanged += OnStateChanged;
        System.Diagnostics.Debug.WriteLine("[Playback] MediaElement connected");
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Playback] State: {e.PreviousState} -> {e.NewState}");
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Playback] MediaEnded fired. Index={CurrentIndex}, QueueSize={Queue.Count}");
        try
        {
            await HandleTrackEndedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Playback] HandleTrackEnded EXCEPTION: {ex}");
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Playback] MediaFailed: {e.ErrorMessage}");
        IsPlaying = false;
        PlaybackStateChanged?.Invoke(this, false);
    }

    private async Task HandleTrackEndedAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[Playback] HandleTrackEnded: RepeatMode={RepeatMode}, Index={CurrentIndex}, QueueSize={Queue.Count}");

        switch (RepeatMode)
        {
            case RepeatMode.One:
                if (_mediaElement != null)
                {
                    _mediaElement.SeekTo(TimeSpan.Zero);
                    _mediaElement.Play();
                }
                break;

            case RepeatMode.All:
                if (CurrentIndex >= Queue.Count - 1)
                {
                    await PlayTrackAtIndexAsync(0);
                }
                else
                {
                    await NextAsync();
                }
                break;

            case RepeatMode.None:
            default:
                if (CurrentIndex < Queue.Count - 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[Playback] Auto-playing next track...");
                    await NextAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Playback] End of queue, stopping.");
                    IsPlaying = false;
                    PlaybackStateChanged?.Invoke(this, false);
                    _positionTimer?.Stop();
                }
                break;
        }
    }

    public async Task PlayAsync(AudioTrack track)
    {
        _originalQueue = [track];
        _shuffledQueue = [track];
        CurrentIndex = 0;
        System.Diagnostics.Debug.WriteLine($"[Playback] PlayAsync(single): {track.Title}");
        await PlayCurrentTrackAsync();
    }

    public async Task PlayAsync(List<AudioTrack> tracks, int startIndex = 0)
    {
        SetQueue(tracks, startIndex);
        System.Diagnostics.Debug.WriteLine($"[Playback] PlayAsync(queue): {tracks.Count} tracks, startIndex={startIndex}");
        await PlayCurrentTrackAsync();
    }

    public Task PauseAsync()
    {
        _mediaElement?.Pause();
        IsPlaying = false;
        _positionTimer?.Stop();
        PlaybackStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _mediaElement?.Play();
        IsPlaying = true;
        _positionTimer?.Start();
        PlaybackStateChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _mediaElement?.Stop();
        IsPlaying = false;
        _positionTimer?.Stop();
        PlaybackStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public async Task NextAsync()
    {
        if (Queue.Count == 0) return;
        var nextIndex = (CurrentIndex + 1) % Queue.Count;
        System.Diagnostics.Debug.WriteLine($"[Playback] NextAsync: {CurrentIndex} -> {nextIndex} (queue={Queue.Count})");
        if (nextIndex == 0 && RepeatMode == RepeatMode.None)
        {
            await StopAsync();
            return;
        }
        await PlayTrackAtIndexAsync(nextIndex);
    }

    public async Task PreviousAsync()
    {
        if (Queue.Count == 0) return;

        // If more than 3 seconds in, restart current track
        if (Position.TotalSeconds > 3)
        {
            await SeekAsync(TimeSpan.Zero);
            return;
        }

        var prevIndex = CurrentIndex - 1;
        if (prevIndex < 0) prevIndex = RepeatMode == RepeatMode.All ? Queue.Count - 1 : 0;
        await PlayTrackAtIndexAsync(prevIndex);
    }

    public Task SeekAsync(TimeSpan position)
    {
        if (_mediaElement != null)
            _mediaElement.SeekTo(position);
        return Task.CompletedTask;
    }

    public void AddToQueue(AudioTrack track)
    {
        _originalQueue.Add(track);
        if (IsShuffleEnabled)
            _shuffledQueue.Add(track);
    }

    public void RemoveFromQueue(int index)
    {
        if (index < 0 || index >= Queue.Count) return;

        var track = Queue[index];
        _originalQueue.Remove(track);
        _shuffledQueue.Remove(track);

        if (index < CurrentIndex)
            CurrentIndex--;
        else if (index == CurrentIndex)
            CurrentIndex = Math.Min(CurrentIndex, Queue.Count - 1);
    }

    public void ClearQueue()
    {
        _originalQueue.Clear();
        _shuffledQueue.Clear();
        CurrentIndex = -1;
    }

    public void SetQueue(List<AudioTrack> tracks, int startIndex = 0)
    {
        _originalQueue = new List<AudioTrack>(tracks);
        CurrentIndex = startIndex;

        if (IsShuffleEnabled)
        {
            _shuffledQueue = new List<AudioTrack>(tracks);
            ShuffleQueue();
        }
        else
        {
            _shuffledQueue = new List<AudioTrack>(tracks);
        }
    }

    private async Task PlayTrackAtIndexAsync(int index)
    {
        if (index < 0 || index >= Queue.Count) return;
        CurrentIndex = index;
        await PlayCurrentTrackAsync();
    }

    private async Task PlayCurrentTrackAsync()
    {
        if (_isLoadingTrack)
        {
            System.Diagnostics.Debug.WriteLine("[Playback] Already loading a track, skipping.");
            return;
        }

        if (CurrentIndex < 0 || CurrentIndex >= Queue.Count)
        {
            System.Diagnostics.Debug.WriteLine($"[Playback] Invalid index: {CurrentIndex}, queue size: {Queue.Count}");
            return;
        }

        _isLoadingTrack = true;

        var track = Queue[CurrentIndex];
        CurrentTrack = track;
        TrackChanged?.Invoke(this, track);

        System.Diagnostics.Debug.WriteLine($"[Playback] === Playing [{CurrentIndex}/{Queue.Count}]: {track.Title} ({track.FileName}) ===");

        try
        {
            // Check if already cached
            var cachedPath = await _cacheService.GetCachedFilePathAsync(track.DriveFileId);
            string sourceUri;

            if (cachedPath != null)
            {
                // Cached: play from local file (instant)
                sourceUri = "file://" + cachedPath;
                System.Diagnostics.Debug.WriteLine($"[Playback] Playing from cache: {cachedPath}");
            }
            else
            {
                // Not cached: stream via local proxy (starts immediately, caches in background)
                var extension = Path.GetExtension(track.FileName);
                sourceUri = _streamingProxy.GetStreamUrl(track.DriveFileId, extension);
                System.Diagnostics.Debug.WriteLine($"[Playback] Streaming via proxy: {sourceUri}");
            }

            // Start playback
            if (_mediaElement != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _mediaElement.Stop();
                    _mediaElement.Source = MediaSource.FromUri(sourceUri);
                    _mediaElement.Play();
                });
                System.Diagnostics.Debug.WriteLine($"[Playback] Play() called OK");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Playback] ERROR: MediaElement is null!");
            }

            IsPlaying = true;
            IsLoading = false;
            LoadingStateChanged?.Invoke(this, false);
            _positionTimer?.Start();
            PlaybackStateChanged?.Invoke(this, true);

            // Extract metadata in background (from cache if available, or after stream caches)
            _ = ExtractMetadataInBackgroundAsync(track, cachedPath);

            // Update play count
            track.LastPlayed = DateTime.UtcNow;
            track.PlayCount++;
            await _databaseService.SaveTrackAsync(track);

            // Pre-cache next track in background
            PreCacheNextTrack();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Playback] EXCEPTION: {ex}");
            IsPlaying = false;
            IsLoading = false;
            LoadingStateChanged?.Invoke(this, false);
            PlaybackStateChanged?.Invoke(this, false);
        }
        finally
        {
            _isLoadingTrack = false;
        }
    }

    private async Task ExtractMetadataInBackgroundAsync(AudioTrack track, string? cachedPath)
    {
        if (track.Duration != TimeSpan.Zero) return;

        try
        {
            // If not cached yet, wait for the proxy to finish caching
            if (cachedPath == null)
            {
                for (int i = 0; i < 60; i++) // Wait up to 30 seconds
                {
                    await Task.Delay(500);
                    cachedPath = await _cacheService.GetCachedFilePathAsync(track.DriveFileId);
                    if (cachedPath != null) break;
                }
            }

            if (cachedPath == null) return;

            await _metadataService.ExtractMetadataAsync(cachedPath, track);
            if (string.IsNullOrEmpty(track.AlbumArtPath))
            {
                track.AlbumArtPath = await _metadataService.ExtractAlbumArtAsync(cachedPath, track.DriveFileId);
            }
            await _databaseService.SaveTrackAsync(track);
            MainThread.BeginInvokeOnMainThread(() => TrackChanged?.Invoke(this, track));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Playback] Metadata extraction failed (non-fatal): {ex.Message}");
        }
    }

    private void PreCacheNextTrack()
    {
        var nextIndex = CurrentIndex + 1;
        if (nextIndex >= Queue.Count) return;

        var nextTrack = Queue[nextIndex];
        _ = Task.Run(async () =>
        {
            try
            {
                await _cacheService.DownloadAndCacheAsync(nextTrack);
                System.Diagnostics.Debug.WriteLine($"[Playback] Pre-cached next track: {nextTrack.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Playback] Pre-cache failed (non-fatal): {ex.Message}");
            }
        });
    }

    private void ShuffleQueue()
    {
        var currentTrack = CurrentIndex >= 0 && CurrentIndex < _shuffledQueue.Count
            ? _shuffledQueue[CurrentIndex] : null;

        var rng = new Random();
        int n = _shuffledQueue.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (_shuffledQueue[k], _shuffledQueue[n]) = (_shuffledQueue[n], _shuffledQueue[k]);
        }

        // Move current track to front
        if (currentTrack != null)
        {
            _shuffledQueue.Remove(currentTrack);
            _shuffledQueue.Insert(0, currentTrack);
            CurrentIndex = 0;
        }
    }
}
