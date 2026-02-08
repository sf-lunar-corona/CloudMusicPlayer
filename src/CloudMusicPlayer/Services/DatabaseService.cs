using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using SQLite;

namespace CloudMusicPlayer.Services;

public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database != null) return _database;

        await _initLock.WaitAsync();
        try
        {
            if (_database != null) return _database;
            await InitializeAsync();
            return _database!;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task InitializeAsync()
    {
        if (_database != null) return;

        _database = new SQLiteAsyncConnection(Constants.DatabasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _database.CreateTableAsync<AudioTrack>();
        await _database.CreateTableAsync<Playlist>();
        await _database.CreateTableAsync<PlaylistTrack>();
        await _database.CreateTableAsync<CachedFile>();
    }

    // AudioTrack operations
    public async Task<List<AudioTrack>> GetAllTracksAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<AudioTrack>().OrderBy(t => t.Title).ToListAsync();
    }

    public async Task<List<AudioTrack>> GetTracksByFolderAsync(string folderId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<AudioTrack>()
            .Where(t => t.FolderId == folderId)
            .OrderBy(t => t.Title)
            .ToListAsync();
    }

    public async Task<AudioTrack?> GetTrackByDriveIdAsync(string driveFileId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<AudioTrack>()
            .FirstOrDefaultAsync(t => t.DriveFileId == driveFileId);
    }

    public async Task<AudioTrack?> GetTrackByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<AudioTrack>().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<int> SaveTrackAsync(AudioTrack track)
    {
        var db = await GetDatabaseAsync();
        if (track.Id != 0)
            return await db.UpdateAsync(track);
        return await db.InsertAsync(track);
    }

    public async Task<int> DeleteTrackAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.DeleteAsync<AudioTrack>(id);
    }

    public async Task DeleteTracksByFolderAsync(string folderId)
    {
        var db = await GetDatabaseAsync();
        await db.ExecuteAsync("DELETE FROM AudioTrack WHERE FolderId = ?", folderId);
    }

    // Playlist operations
    public async Task<List<Playlist>> GetAllPlaylistsAsync()
    {
        var db = await GetDatabaseAsync();
        var playlists = await db.Table<Playlist>().OrderBy(p => p.Name).ToListAsync();
        foreach (var playlist in playlists)
        {
            var tracks = await db.Table<PlaylistTrack>()
                .Where(pt => pt.PlaylistId == playlist.Id)
                .ToListAsync();
            playlist.TrackCount = tracks.Count;
        }
        return playlists;
    }

    public async Task<Playlist?> GetPlaylistByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<Playlist>().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<int> SavePlaylistAsync(Playlist playlist)
    {
        var db = await GetDatabaseAsync();
        playlist.UpdatedAt = DateTime.UtcNow;
        if (playlist.Id != 0)
            return await db.UpdateAsync(playlist);
        return await db.InsertAsync(playlist);
    }

    public async Task<int> DeletePlaylistAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await DeletePlaylistTracksAsync(id);
        return await db.DeleteAsync<Playlist>(id);
    }

    // PlaylistTrack operations
    public async Task<List<PlaylistTrack>> GetPlaylistTracksAsync(int playlistId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PlaylistTrack>()
            .Where(pt => pt.PlaylistId == playlistId)
            .OrderBy(pt => pt.SortOrder)
            .ToListAsync();
    }

    public async Task<int> SavePlaylistTrackAsync(PlaylistTrack playlistTrack)
    {
        var db = await GetDatabaseAsync();
        if (playlistTrack.Id != 0)
            return await db.UpdateAsync(playlistTrack);
        return await db.InsertAsync(playlistTrack);
    }

    public async Task<int> DeletePlaylistTrackAsync(int playlistId, int trackId)
    {
        var db = await GetDatabaseAsync();
        return await db.ExecuteAsync(
            "DELETE FROM PlaylistTrack WHERE PlaylistId = ? AND TrackId = ?",
            playlistId, trackId);
    }

    public async Task DeletePlaylistTracksAsync(int playlistId)
    {
        var db = await GetDatabaseAsync();
        await db.ExecuteAsync(
            "DELETE FROM PlaylistTrack WHERE PlaylistId = ?", playlistId);
    }

    // CachedFile operations
    public async Task<CachedFile?> GetCachedFileAsync(string driveFileId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<CachedFile>()
            .FirstOrDefaultAsync(c => c.DriveFileId == driveFileId);
    }

    public async Task<List<CachedFile>> GetAllCachedFilesAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<CachedFile>().OrderBy(c => c.LastAccessed).ToListAsync();
    }

    public async Task<long> GetTotalCacheSizeAsync()
    {
        var db = await GetDatabaseAsync();
        var result = await db.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(FileSize), 0) FROM CachedFile");
        return result;
    }

    public async Task<int> SaveCachedFileAsync(CachedFile cachedFile)
    {
        var db = await GetDatabaseAsync();
        if (cachedFile.Id != 0)
            return await db.UpdateAsync(cachedFile);
        return await db.InsertAsync(cachedFile);
    }

    public async Task<int> DeleteCachedFileAsync(string driveFileId)
    {
        var db = await GetDatabaseAsync();
        return await db.ExecuteAsync(
            "DELETE FROM CachedFile WHERE DriveFileId = ?", driveFileId);
    }

    public async Task ClearCachedFilesAsync()
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAllAsync<CachedFile>();
    }

    // Search
    public async Task<List<AudioTrack>> SearchTracksAsync(string query)
    {
        var db = await GetDatabaseAsync();
        var lowerQuery = $"%{query.ToLowerInvariant()}%";
        return await db.QueryAsync<AudioTrack>(
            "SELECT * FROM AudioTrack WHERE LOWER(Title) LIKE ? OR LOWER(Artist) LIKE ? OR LOWER(Album) LIKE ? ORDER BY Title",
            lowerQuery, lowerQuery, lowerQuery);
    }

    // Favorites
    public async Task<List<AudioTrack>> GetFavoriteTracksAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<AudioTrack>()
            .Where(t => t.IsFavorite)
            .OrderBy(t => t.Title)
            .ToListAsync();
    }

    public async Task UpdateTrackFavoriteAsync(int trackId, bool isFavorite)
    {
        var db = await GetDatabaseAsync();
        await db.ExecuteAsync(
            "UPDATE AudioTrack SET IsFavorite = ? WHERE Id = ?",
            isFavorite, trackId);
    }
}
