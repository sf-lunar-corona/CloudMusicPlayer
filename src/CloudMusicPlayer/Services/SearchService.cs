using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class SearchService : ISearchService
{
    private readonly IDatabaseService _databaseService;

    public SearchService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<AudioTrack>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        return await _databaseService.SearchTracksAsync(query.Trim());
    }

    public async Task<List<AudioTrack>> SearchByArtistAsync(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return [];

        var allTracks = await _databaseService.GetAllTracksAsync();
        return allTracks
            .Where(t => t.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<List<AudioTrack>> SearchByAlbumAsync(string album)
    {
        if (string.IsNullOrWhiteSpace(album))
            return [];

        var allTracks = await _databaseService.GetAllTracksAsync();
        return allTracks
            .Where(t => t.Album.Contains(album, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
