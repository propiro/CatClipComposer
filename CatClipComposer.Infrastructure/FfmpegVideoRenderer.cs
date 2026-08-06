using System.Diagnostics;
using System.Globalization;
using System.Text;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class FfmpegVideoRenderer : IVideoRenderer
{
    public async Task<RenderResult> RenderAsync(
        RenderRequest request,
        string ffmpegPath,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Segments.Count == 0)
        {
            throw new ArgumentException("At least one timeline segment is required.", nameof(request));
        }

        var invalidSegment = request.Segments.FirstOrDefault(segment =>
            segment.Duration <= TimeSpan.Zero || !File.Exists(segment.SourcePath));
        if (invalidSegment is not null)
        {
            throw new InvalidOperationException(
                $"A timeline source is missing or has no duration: {invalidSegment.SourcePath}");
        }

        var hasImageOverlay = !string.IsNullOrWhiteSpace(request.OverlayImagePath);
        if (hasImageOverlay && !File.Exists(request.OverlayImagePath))
        {
            throw new InvalidOperationException($"The overlay image does not exist: {request.OverlayImagePath}");
        }

        if (!string.IsNullOrWhiteSpace(request.OverlayFontPath) && !File.Exists(request.OverlayFontPath))
        {
            throw new InvalidOperationException($"The overlay font does not exist: {request.OverlayFontPath}");
        }

        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("The output path must include a folder.", nameof(request));
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = Path.GetExtension(request.OutputPath);
        var operationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(request.OutputPath)}.{operationId}.partial{extension}");
        string? overlayTextFile = null;
        if (!string.IsNullOrWhiteSpace(request.OverlayText))
        {
            overlayTextFile = Path.Combine(outputDirectory, $".overlay-{operationId}.txt");
            await File.WriteAllTextAsync(
                overlayTextFile,
                request.OverlayText,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }

        var totalDuration = TimeSpan.FromTicks(request.Segments.Sum(segment => segment.Duration.Ticks));
        var (width, height) = request.Orientation == OutputOrientation.Portrait
            ? (1080, 1920)
            : (1920, 1080);
        var startInfo = BuildStartInfo(
            request,
            ffmpegPath,
            temporaryPath,
            overlayTextFile,
            hasImageOverlay,
            width,
            height);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            try
            {
                process.Start();
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                throw new InvalidOperationException(
                    $"FFmpeg was not found. Configure ffmpeg.exe in Options. Tried: {startInfo.FileName}",
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

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("out_time=", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = line["out_time=".Length..];
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var processed))
                {
                    continue;
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

            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;
            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0 || !File.Exists(temporaryPath))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"FFmpeg stopped with exit code {process.ExitCode}."
                        : error.Trim());
            }

            File.Move(temporaryPath, request.OutputPath, overwrite: true);
            progress?.Report(new RenderProgress(
                100,
                totalDuration,
                totalDuration,
                "Compilation complete"));
            return new RenderResult(request.OutputPath, totalDuration);
        }
        finally
        {
            TryDelete(temporaryPath);
            TryDelete(overlayTextFile);
        }
    }

    private static ProcessStartInfo BuildStartInfo(
        RenderRequest request,
        string configuredFfmpegPath,
        string temporaryPath,
        string? overlayTextFile,
        bool hasImageOverlay,
        int width,
        int height)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegToolPaths.ResolveFfmpeg(configuredFfmpegPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-y");
        foreach (var segment in request.Segments)
        {
            if (segment.Kind == RenderSegmentKind.StillImage)
            {
                startInfo.ArgumentList.Add("-loop");
                startInfo.ArgumentList.Add("1");
                startInfo.ArgumentList.Add("-framerate");
                startInfo.ArgumentList.Add(request.FramesPerSecond.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add(segment.Duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture));
            }

            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(segment.SourcePath);
        }

        if (hasImageOverlay)
        {
            startInfo.ArgumentList.Add("-loop");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-framerate");
            startInfo.ArgumentList.Add(request.FramesPerSecond.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(request.OverlayImagePath!);
        }

        startInfo.ArgumentList.Add("-filter_complex");
        startInfo.ArgumentList.Add(BuildFilterGraph(
            request,
            width,
            height,
            overlayTextFile,
            hasImageOverlay));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[outv]");
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[outa]");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("medium");
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add("20");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-b:a");
        startInfo.ArgumentList.Add("192k");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add("-progress");
        startInfo.ArgumentList.Add("pipe:1");
        startInfo.ArgumentList.Add("-nostats");
        startInfo.ArgumentList.Add(temporaryPath);
        return startInfo;
    }

    private static string BuildFilterGraph(
        RenderRequest request,
        int width,
        int height,
        string? overlayTextFile,
        bool hasImageOverlay)
    {
        var graph = new StringBuilder();
        for (var index = 0; index < request.Segments.Count; index++)
        {
            var segment = request.Segments[index];
            var seconds = segment.Duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
            var videoOutputLabel = request.ProgressStyle == VideoProgressStyle.EachClip
                ? $"basev{index}"
                : $"v{index}";
            graph.Append(CultureInfo.InvariantCulture, $"[{index}:v:0]");
            graph.Append(CultureInfo.InvariantCulture,
                $"scale={width}:{height}:force_original_aspect_ratio=decrease,");
            graph.Append(CultureInfo.InvariantCulture,
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,");
            graph.Append(CultureInfo.InvariantCulture,
                $"setsar=1,fps={request.FramesPerSecond},format=yuv420p,");
            graph.Append(CultureInfo.InvariantCulture,
                $"trim=duration={seconds},setpts=PTS-STARTPTS[{videoOutputLabel}];");

            if (request.ProgressStyle == VideoProgressStyle.EachClip)
            {
                graph.Append(CultureInfo.InvariantCulture,
                    $"color=c=0x7BD88F@0.92:s={width}x12:r={request.FramesPerSecond}:d={seconds}[bar{index}];");
                graph.Append(CultureInfo.InvariantCulture,
                    $"[basev{index}][bar{index}]overlay=x='-W+W*min(t/{seconds},1)':y=H-h:shortest=1[v{index}];");
            }

            if (segment.HasAudio)
            {
                graph.Append(CultureInfo.InvariantCulture, $"[{index}:a:0]");
                graph.Append("aresample=48000,");
                graph.Append("aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,");
                graph.Append(CultureInfo.InvariantCulture, $"atrim=duration={seconds},");
                graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
            }
            else
            {
                graph.Append(CultureInfo.InvariantCulture,
                    $"anullsrc=r=48000:cl=stereo,atrim=duration={seconds},");
                graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
            }
        }

        for (var index = 0; index < request.Segments.Count; index++)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[v{index}][a{index}]");
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"concat=n={request.Segments.Count}:v=1:a=1[joinedv][outa];");
        var currentLabel = "joinedv";
        var stage = 0;

        if (hasImageOverlay)
        {
            var overlayInputIndex = request.Segments.Count;
            graph.Append(CultureInfo.InvariantCulture,
                $"[{overlayInputIndex}:v:0]scale='min(360,iw)':-2,format=rgba,colorchannelmixer=aa=0.85[watermark];");
            var (x, y) = GetImageOverlayCoordinates(request.OverlayPosition);
            graph.Append(CultureInfo.InvariantCulture,
                $"[{currentLabel}][watermark]overlay=x={x}:y={y}:shortest=1[stage{stage}];");
            currentLabel = $"stage{stage++}";
        }

        if (!string.IsNullOrWhiteSpace(overlayTextFile))
        {
            var (x, y) = GetTextOverlayCoordinates(request.OverlayPosition);
            var fontOption = string.IsNullOrWhiteSpace(request.OverlayFontPath)
                ? "font='Segoe UI':"
                : $"fontfile='{EscapeFilterValue(request.OverlayFontPath)}':";
            graph.Append(CultureInfo.InvariantCulture,
                $"[{currentLabel}]drawtext=textfile='{EscapeFilterValue(overlayTextFile)}':{fontOption}");
            graph.Append(CultureInfo.InvariantCulture,
                $"fontcolor=white:fontsize={Math.Clamp(request.OverlayTextSize, 8, 200)}:");
            graph.Append(CultureInfo.InvariantCulture,
                $"borderw=3:bordercolor=black@0.7:x={x}:y={y}[stage{stage}];");
            currentLabel = $"stage{stage++}";
        }

        if (request.ProgressStyle == VideoProgressStyle.WholeCompilation)
        {
            var totalSeconds = request.Segments.Sum(segment => segment.Duration.TotalSeconds)
                .ToString("0.######", CultureInfo.InvariantCulture);
            graph.Append(CultureInfo.InvariantCulture,
                $"color=c=0x7BD88F@0.92:s={width}x12:r={request.FramesPerSecond}:d={totalSeconds}[wholebar];");
            graph.Append(CultureInfo.InvariantCulture,
                $"[{currentLabel}][wholebar]overlay=x='-W+W*min(t/{totalSeconds},1)':y=H-h:shortest=1[outv]");
        }
        else
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{currentLabel}]null[outv]");
        }

        return graph.ToString();
    }

    private static (string X, string Y) GetImageOverlayCoordinates(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => ("32", "32"),
        OverlayPosition.TopRight => ("W-w-32", "32"),
        OverlayPosition.BottomLeft => ("32", "H-h-32"),
        OverlayPosition.BottomRight => ("W-w-32", "H-h-32"),
        _ => ("(W-w)/2", "(H-h)/2")
    };

    private static (string X, string Y) GetTextOverlayCoordinates(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => ("32", "32"),
        OverlayPosition.TopRight => ("w-text_w-32", "32"),
        OverlayPosition.BottomLeft => ("32", "h-text_h-32"),
        OverlayPosition.BottomRight => ("w-text_w-32", "h-text_h-32"),
        _ => ("(w-text_w)/2", "(h-text_h)/2")
    };

    private static string EscapeFilterValue(string value) => value
        .Replace("\\", "/", StringComparison.Ordinal)
        .Replace(":", "\\:", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal);

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
