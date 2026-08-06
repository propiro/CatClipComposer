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
        string? overlayTextPath,
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
                "-framerate", request.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                "-i", request.OverlayImagePath!);
        }

        AddArguments(
            startInfo,
            "-filter_complex",
            FfmpegFilterGraphBuilder.Build(
                request,
                width,
                height,
                overlayTextPath,
                hasImageOverlay),
            "-map", "[outv]",
            "-map", "[outa]");
        AddVideoEncoder(startInfo, request.VideoEncoder);
        AddArguments(
            startInfo,
            "-c:a", "aac",
            "-b:a", "192k",
            "-movflags", "+faststart",
            "-progress", "pipe:1",
            "-nostats",
            temporaryOutputPath);
        return startInfo;
    }

    private static void AddVideoEncoder(ProcessStartInfo startInfo, VideoEncoderPreset preset)
    {
        switch (preset)
        {
            case VideoEncoderPreset.NativeMpeg4:
                AddArguments(startInfo, "-c:v", "mpeg4", "-q:v", "3", "-bf", "2");
                break;
            case VideoEncoderPreset.WindowsMediaFoundationH264:
                AddArguments(
                    startInfo,
                    "-c:v", "h264_mf",
                    "-rate_control", "quality",
                    "-quality", "80",
                    "-scenario", "archive");
                break;
            case VideoEncoderPreset.Libx264Gpl:
                AddArguments(
                    startInfo,
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "20");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported video encoder preset.");
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
                    "-framerate", request.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                    "-t", segment.Duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture));
            }

            AddArguments(startInfo, "-i", segment.SourcePath);
        }
    }

    private static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }
}
