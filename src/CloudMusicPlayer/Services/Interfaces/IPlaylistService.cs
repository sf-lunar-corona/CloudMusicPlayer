using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IPlaylistService
{
    Task<List<Playlist>> GetAllPlaylistsAsync();
    Task<Playlist?> GetPlaylistAsync(int id);
    Task<Playlist> CreatePlaylistAsync(string name, string? description = null);
    Task UpdatePlaylistAsync(Playlist playlist);
    Task DeletePlaylistAsync(int id);
    Task AddTrackToPlaylistAsync(int playlistId, int trackId);
    Task RemoveTrackFromPlaylistAsync(int playlistId, int trackId);
    Task<List<AudioTrack>> GetPlaylistTracksAsync(int playlistId);
    Task ReorderTrackAsync(int playlistId, int trackId, int newOrder);
}
