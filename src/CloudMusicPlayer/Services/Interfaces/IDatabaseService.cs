using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync();

    // AudioTrack operations
    Task<List<AudioTrack>> GetAllTracksAsync();
    Task<List<AudioTrack>> GetTracksByFolderAsync(string folderId);
    Task<AudioTrack?> GetTrackByDriveIdAsync(string driveFileId);
    Task<AudioTrack?> GetTrackByIdAsync(int id);
    Task<int> SaveTrackAsync(AudioTrack track);
    Task<int> DeleteTrackAsync(int id);
    Task DeleteTracksByFolderAsync(string folderId);

    // Playlist operations
    Task<List<Playlist>> GetAllPlaylistsAsync();
    Task<Playlist?> GetPlaylistByIdAsync(int id);
    Task<int> SavePlaylistAsync(Playlist playlist);
    Task<int> DeletePlaylistAsync(int id);

    // PlaylistTrack operations
    Task<List<PlaylistTrack>> GetPlaylistTracksAsync(int playlistId);
    Task<int> SavePlaylistTrackAsync(PlaylistTrack playlistTrack);
    Task<int> DeletePlaylistTrackAsync(int playlistId, int trackId);
    Task DeletePlaylistTracksAsync(int playlistId);

    // CachedFile operations
    Task<CachedFile?> GetCachedFileAsync(string driveFileId);
    Task<List<CachedFile>> GetAllCachedFilesAsync();
    Task<long> GetTotalCacheSizeAsync();
    Task<int> SaveCachedFileAsync(CachedFile cachedFile);
    Task<int> DeleteCachedFileAsync(string driveFileId);
    Task ClearCachedFilesAsync();

    // Search
    Task<List<AudioTrack>> SearchTracksAsync(string query);

    // Favorites
    Task<List<AudioTrack>> GetFavoriteTracksAsync();
    Task UpdateTrackFavoriteAsync(int trackId, bool isFavorite);

    // Albums
    Task<List<AlbumInfo>> GetAlbumsAsync(IEnumerable<string>? folderIds = null);
    Task<List<AudioTrack>> GetTracksByAlbumAsync(string artist, string albumName);
    Task<List<AudioTrack>> GetTracksWithDefaultMetadataAsync(IEnumerable<string>? folderIds = null);
}
