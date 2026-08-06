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
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        Start(process);
        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            ReportProgress(line, totalDuration, progress);
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
                $"FFmpeg was not found. Configure ffmpeg.exe in Options. Tried: {process.StartInfo.FileName}",
                exception);
        }
    }

    private static void ReportProgress(
        string line,
        TimeSpan totalDuration,
        IProgress<RenderProgress>? progress)
    {
        if (!line.StartsWith("out_time=", StringComparison.Ordinal) ||
            !TimeSpan.TryParse(line["out_time=".Length..], CultureInfo.InvariantCulture, out var processed))
        {
            return;
        }

        var percent = totalDuration <= TimeSpan.Zero
            ? 0
            : Math.Clamp(processed.TotalMilliseconds / totalDuration.TotalMilliseconds * 100, 0, 99.5);
        progress?.Report(new RenderProgress(
            percent,
            processed,
            totalDuration,
            $"Rendering {percent:0}%"));
    }

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
