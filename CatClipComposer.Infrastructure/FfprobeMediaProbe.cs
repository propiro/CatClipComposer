using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class FfprobeMediaProbe : IMediaProbe
{
    public async Task<VideoMetadata> ProbeAsync(
        string filePath,
        string ffmpegPath,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegToolPaths.ResolveFfprobe(ffmpegPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration:stream=codec_type,width,height");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(filePath);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"FFprobe was not found. Configure the FFmpeg executable in Preferences. Tried: {startInfo.FileName}",
                exception);
        }

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

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFprobe could not read '{Path.GetFileName(filePath)}': {error.Trim()}");
        }

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        var duration = ReadDuration(root);
        var (width, height, hasAudio) = ReadStreams(root);

        if (duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"FFprobe reported no usable duration for '{Path.GetFileName(filePath)}'.");
        }

        return new VideoMetadata(duration, width, height, hasAudio);
    }

    private static TimeSpan ReadDuration(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var format) ||
            !format.TryGetProperty("duration", out var durationElement))
        {
            return TimeSpan.Zero;
        }

        var value = durationElement.ValueKind == JsonValueKind.String
            ? durationElement.GetString()
            : durationElement.GetRawText();

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
    }

    private static (int Width, int Height, bool HasAudio) ReadStreams(JsonElement root)
    {
        var width = 0;
        var height = 0;
        var hasAudio = false;

        if (!root.TryGetProperty("streams", out var streams))
        {
            return (width, height, hasAudio);
        }

        foreach (var stream in streams.EnumerateArray())
        {
            var type = stream.TryGetProperty("codec_type", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase))
            {
                hasAudio = true;
            }
            else if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase) && width == 0)
            {
                width = stream.TryGetProperty("width", out var widthElement)
                    ? widthElement.GetInt32()
                    : 0;
                height = stream.TryGetProperty("height", out var heightElement)
                    ? heightElement.GetInt32()
                    : 0;
            }
        }

        return (width, height, hasAudio);
    }
}
