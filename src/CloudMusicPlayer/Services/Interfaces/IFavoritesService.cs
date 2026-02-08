using CloudMusicPlayer.Models;

namespace CloudMusicPlayer.Services.Interfaces;

public interface IFavoritesService
{
    Task<List<AudioTrack>> GetFavoritesAsync();
    Task ToggleFavoriteAsync(AudioTrack track);
    Task<bool> IsFavoriteAsync(int trackId);
}
