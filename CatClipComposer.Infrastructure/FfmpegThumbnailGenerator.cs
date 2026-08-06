using System.Diagnostics;
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
        CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{filePath.ToUpperInvariant()}|{lastWriteUtc.Ticks}")));
        var outputPath = Path.Combine(paths.ThumbnailFolder, $"{cacheKey}.jpg");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var seekSeconds = Math.Clamp(duration.TotalSeconds * 0.15, 0, 5);
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegToolPaths.ResolveFfmpeg(ffmpegPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-hide_banner", "-loglevel", "error",
                     "-ss", seekSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                     "-i", filePath,
                     "-frames:v", "1",
                     "-vf", "scale=320:-2",
                     "-q:v", "3",
                     "-y", outputPath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && File.Exists(outputPath) ? outputPath : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return null;
        }
    }
}
