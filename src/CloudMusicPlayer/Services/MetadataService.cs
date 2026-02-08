using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;

namespace CloudMusicPlayer.Services;

public class MetadataService : IMetadataService
{
    public async Task<AudioTrack> ExtractMetadataAsync(string filePath, AudioTrack track)
    {
        await Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(filePath);

                if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                    track.Title = file.Tag.Title;
                else if (string.IsNullOrEmpty(track.Title))
                    track.Title = Path.GetFileNameWithoutExtension(track.FileName);

                if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
                    track.Artist = file.Tag.FirstPerformer;
                else if (!string.IsNullOrWhiteSpace(file.Tag.FirstAlbumArtist))
                    track.Artist = file.Tag.FirstAlbumArtist;

                if (!string.IsNullOrWhiteSpace(file.Tag.Album))
                    track.Album = file.Tag.Album;

                track.TrackNumber = (int)file.Tag.Track;
                track.Duration = file.Properties.Duration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metadata extraction failed for {filePath}: {ex.Message}");
                if (string.IsNullOrEmpty(track.Title))
                    track.Title = Path.GetFileNameWithoutExtension(track.FileName);
            }
        });

        return track;
    }

    public async Task<string?> ExtractAlbumArtAsync(string filePath, string driveFileId)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                var pictures = file.Tag.Pictures;

                if (pictures == null || pictures.Length == 0)
                    return null;

                var picture = pictures[0];
                var artDir = Path.Combine(Constants.CacheDirectory, "albumart");
                if (!Directory.Exists(artDir))
                    Directory.CreateDirectory(artDir);

                var extension = picture.MimeType switch
                {
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    _ => ".jpg"
                };

                var artPath = Path.Combine(artDir, $"{driveFileId}{extension}");

                if (!File.Exists(artPath))
                    File.WriteAllBytes(artPath, picture.Data.Data);

                return artPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Album art extraction failed: {ex.Message}");
                return null;
            }
        });
    }
}
