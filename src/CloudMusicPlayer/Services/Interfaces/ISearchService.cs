using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface ISearchService
{
    Task<List<AudioTrack>> SearchAsync(string query);
    Task<List<AudioTrack>> SearchByArtistAsync(string artist);
    Task<List<AudioTrack>> SearchByAlbumAsync(string album);
}
