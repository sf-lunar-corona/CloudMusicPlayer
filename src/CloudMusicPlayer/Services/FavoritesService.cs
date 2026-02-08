using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class FavoritesService : IFavoritesService
{
    private readonly IDatabaseService _databaseService;

    public FavoritesService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<AudioTrack>> GetFavoritesAsync()
    {
        return await _databaseService.GetFavoriteTracksAsync();
    }

    public async Task ToggleFavoriteAsync(AudioTrack track)
    {
        track.IsFavorite = !track.IsFavorite;
        await _databaseService.UpdateTrackFavoriteAsync(track.Id, track.IsFavorite);
    }

    public async Task<bool> IsFavoriteAsync(int trackId)
    {
        var track = await _databaseService.GetTrackByIdAsync(trackId);
        return track?.IsFavorite ?? false;
    }
}
