using System.Diagnostics;
using System.Globalization;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Infrastructure.Rendering;

internal sealed class FfmpegRenderCommandBuilder
{
    public ProcessStartInfo Build(
        RenderRequest request,
        string configuredFfmpegPath,
        string temporaryOutputPath,
        string? legacyOverlayTextPath,
        IReadOnlyDictionary<int, string> timedTextPaths,
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
        AddArguments(startInfo, "-hide_banner", "-loglevel", "error", "-y");
        AddInputs(startInfo, request);

        var hasImageOverlay = !string.IsNullOrWhiteSpace(request.OverlayImagePath);
        if (hasImageOverlay)
        {
            AddArguments(
                startInfo,
                "-loop", "1",
                "-framerate", FormatNumber(request.FramesPerSecond),
                "-i", request.OverlayImagePath!);
        }

        foreach (var overlay in (request.TimedOverlays ?? []).Where(item => item.Kind == RenderOverlayKind.Image))
        {
            AddArguments(
                startInfo,
                "-loop", "1",
                "-framerate", FormatNumber(request.FramesPerSecond),
                "-i", overlay.SourcePath!);
        }

        foreach (var audioLayer in request.AudioLayers ?? [])
        {
            AddArguments(startInfo, "-stream_loop", "-1", "-i", audioLayer.SourcePath);
        }

        AddArguments(
            startInfo,
            "-filter_complex",
            FfmpegFilterGraphBuilder.Build(
                request,
                width,
                height,
                legacyOverlayTextPath,
                timedTextPaths,
                hasImageOverlay),
            "-map", "[outv]",
            "-map", "[outa]");
        AddVideoEncoder(startInfo, request);
        AddArguments(
            startInfo,
            "-c:a", "aac",
            "-b:a", $"{Math.Clamp(request.AudioBitrateKbps, 64, 512)}k",
            "-movflags", "+faststart",
            "-progress", "pipe:1",
            "-nostats",
            temporaryOutputPath);
        return startInfo;
    }

    private static void AddVideoEncoder(ProcessStartInfo startInfo, RenderRequest request)
    {
        var bitrate = $"{Math.Clamp(request.VideoBitrateKbps, 500, 150000)}k";
        var quality = Math.Clamp(request.QualityPercent, 1, 100);
        switch (request.VideoEncoder)
        {
            case VideoEncoderPreset.NativeMpeg4:
                var nativeQMin = Math.Clamp(
                    (int)Math.Round(31 - quality * 0.29),
                    2,
                    30);
                AddArguments(
                    startInfo,
                    "-c:v", "mpeg4",
                    "-b:v", bitrate,
                    "-qmin", nativeQMin.ToString(CultureInfo.InvariantCulture),
                    "-qmax", Math.Min(31, nativeQMin + 6).ToString(CultureInfo.InvariantCulture),
                    "-bf", "2");
                break;
            case VideoEncoderPreset.WindowsMediaFoundationH264:
                AddArguments(
                    startInfo,
                    "-c:v", "h264_mf",
                    "-rate_control", "quality",
                    "-quality", quality.ToString(CultureInfo.InvariantCulture),
                    "-scenario", "archive");
                break;
            case VideoEncoderPreset.Libx264Gpl:
                AddArguments(
                    startInfo,
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", Math.Clamp(
                        (int)Math.Round(36 - quality * 0.24),
                        12,
                        35).ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request.VideoEncoder),
                    request.VideoEncoder,
                    "Unsupported video encoder preset.");
        }
    }

    private static void AddInputs(ProcessStartInfo startInfo, RenderRequest request)
    {
        foreach (var segment in request.Segments)
        {
            if (segment.Kind == RenderSegmentKind.StillImage)
            {
                AddArguments(
                    startInfo,
                    "-loop", "1",
                    "-framerate", FormatNumber(request.FramesPerSecond),
                    "-t", segment.Duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture));
            }

            AddArguments(startInfo, "-i", segment.SourcePath);
        }
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }
}
