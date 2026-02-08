using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class PlaylistService : IPlaylistService
{
    private readonly IDatabaseService _databaseService;

    public PlaylistService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<Playlist>> GetAllPlaylistsAsync()
    {
        return await _databaseService.GetAllPlaylistsAsync();
    }

    public async Task<Playlist?> GetPlaylistAsync(int id)
    {
        var playlist = await _databaseService.GetPlaylistByIdAsync(id);
        if (playlist != null)
        {
            playlist.Tracks = await GetPlaylistTracksAsync(id);
            playlist.TrackCount = playlist.Tracks.Count;
        }
        return playlist;
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
    {
        var playlist = new Playlist
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _databaseService.SavePlaylistAsync(playlist);
        return playlist;
    }

    public async Task UpdatePlaylistAsync(Playlist playlist)
    {
        playlist.UpdatedAt = DateTime.UtcNow;
        await _databaseService.SavePlaylistAsync(playlist);
    }

    public async Task DeletePlaylistAsync(int id)
    {
        await _databaseService.DeletePlaylistAsync(id);
    }

    public async Task AddTrackToPlaylistAsync(int playlistId, int trackId)
    {
        var existingTracks = await _databaseService.GetPlaylistTracksAsync(playlistId);

        // Don't add duplicates
        if (existingTracks.Any(pt => pt.TrackId == trackId))
            return;

        var playlistTrack = new PlaylistTrack
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            SortOrder = existingTracks.Count,
            AddedAt = DateTime.UtcNow
        };

        await _databaseService.SavePlaylistTrackAsync(playlistTrack);
    }

    public async Task RemoveTrackFromPlaylistAsync(int playlistId, int trackId)
    {
        await _databaseService.DeletePlaylistTrackAsync(playlistId, trackId);
    }

    public async Task<List<AudioTrack>> GetPlaylistTracksAsync(int playlistId)
    {
        var playlistTracks = await _databaseService.GetPlaylistTracksAsync(playlistId);
        var tracks = new List<AudioTrack>();

        foreach (var pt in playlistTracks.OrderBy(pt => pt.SortOrder))
        {
            var track = await _databaseService.GetTrackByIdAsync(pt.TrackId);
            if (track != null)
                tracks.Add(track);
        }

        return tracks;
    }

    public async Task ReorderTrackAsync(int playlistId, int trackId, int newOrder)
    {
        var playlistTracks = await _databaseService.GetPlaylistTracksAsync(playlistId);
        var target = playlistTracks.FirstOrDefault(pt => pt.TrackId == trackId);
        if (target == null) return;

        target.SortOrder = newOrder;
        await _databaseService.SavePlaylistTrackAsync(target);

        // Reorder other tracks
        var sorted = playlistTracks.OrderBy(pt => pt.SortOrder).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].SortOrder != i)
            {
                sorted[i].SortOrder = i;
                await _databaseService.SavePlaylistTrackAsync(sorted[i]);
            }
        }
    }
}
