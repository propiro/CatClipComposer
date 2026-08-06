using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class FfmpegThumbnailGenerator(AppPaths paths) : IThumbnailGenerator
{
    public async Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        string ffmpegPath,
        bool forceRecreate = false,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{filePath.ToUpperInvariant()}|{lastWriteUtc.Ticks}")));
        var outputPath = Path.Combine(paths.ThumbnailFolder, $"{cacheKey}.jpg");
        if (!forceRecreate && File.Exists(outputPath))
        {
            return outputPath;
        }

        var seekSeconds = Math.Clamp(duration.TotalSeconds * 0.15, 0, 5);
        var created = await FfmpegPreviewImageWriter.RunAsync(
            ffmpegPath,
            [
                "-hide_banner", "-loglevel", "error",
                "-ss", seekSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", filePath,
                "-frames:v", "1",
                "-vf", "scale=320:-2",
                "-q:v", "3",
                "-y", outputPath
            ],
            outputPath,
            cancellationToken);
        return created ? outputPath : null;
    }
}
