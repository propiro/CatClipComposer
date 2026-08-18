using System.Diagnostics;
using System.Globalization;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Infrastructure.Rendering;

internal sealed record FfmpegProcessResult(int ExitCode, string StandardError);

internal sealed class FfmpegProcessRunner
{
    public async Task<FfmpegProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan totalDuration,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken,
        double startPercent = 0,
        double endPercent = 99.5)
    {
        using var process = new Process { StartInfo = startInfo };
        Start(process);
        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            ReportProgress(line, totalDuration, progress, startPercent, endPercent);
        }

        await process.WaitForExitAsync(cancellationToken);
        return new FfmpegProcessResult(process.ExitCode, await errorTask);
    }

    private static void Start(Process process)
    {
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"FFmpeg was not found. Configure ffmpeg.exe in Preferences. Tried: {process.StartInfo.FileName}",
                exception);
        }
    }

    private static void ReportProgress(
        string line,
        TimeSpan totalDuration,
        IProgress<RenderProgress>? progress,
        double startPercent,
        double endPercent)
    {
        TimeSpan processed;
        if (line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
            long.TryParse(line["out_time_us=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
        {
            processed = TimeSpan.FromTicks(microseconds * 10);
        }
        else if (line.StartsWith("out_time_ms=", StringComparison.Ordinal) &&
                 long.TryParse(line["out_time_ms=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyMicroseconds))
        {
            // FFmpeg's historical out_time_ms field is actually expressed in microseconds.
            processed = TimeSpan.FromTicks(legacyMicroseconds * 10);
        }
        else if (line.StartsWith("out_time=", StringComparison.Ordinal) &&
                 TimeSpan.TryParse(line["out_time=".Length..], CultureInfo.InvariantCulture, out var timestamp))
        {
            processed = timestamp;
        }
        else
        {
            return;
        }

        var encodedFraction = totalDuration <= TimeSpan.Zero
            ? 0
            : Math.Clamp(processed.TotalMilliseconds / totalDuration.TotalMilliseconds, 0, 0.995);
        var percent = startPercent + ((endPercent - startPercent) * encodedFraction);
        progress?.Report(new RenderProgress(
            percent,
            processed,
            totalDuration,
            $"FFmpeg encoding {FormatDuration(processed)} of {FormatDuration(totalDuration)}"));
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static void TryKill(Process process)
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
    }
}
