using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class FfmpegPreviewSheetGenerator(AppPaths paths) : IPreviewSheetGenerator
{
    public async Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        int slideCount,
        string ffmpegPath,
        bool forceRecreate = false,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

        paths.EnsureCreated();
        slideCount = Math.Clamp(slideCount, 1, 12);
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{filePath.ToUpperInvariant()}|{lastWriteUtc.Ticks}|{slideCount}")));
        var outputPath = Path.Combine(paths.PreviewFolder, $"{cacheKey}.jpg");
        if (!forceRecreate && File.Exists(outputPath))
        {
            return outputPath;
        }

        var sampleRate = slideCount / Math.Max(duration.TotalSeconds, 0.001);
        var filter = string.Create(
            CultureInfo.InvariantCulture,
            $"fps={sampleRate:0.########},scale=160:90:force_original_aspect_ratio=decrease," +
            $"pad=160:90:(ow-iw)/2:(oh-ih)/2:color=black,tile={slideCount}x1");
        var created = await FfmpegPreviewImageWriter.RunAsync(
            ffmpegPath,
            [
                "-hide_banner", "-loglevel", "error",
                "-i", filePath,
                "-frames:v", "1",
                "-vf", filter,
                "-q:v", "3",
                "-y", outputPath
            ],
            outputPath,
            cancellationToken);
        return created ? outputPath : null;
    }
}
