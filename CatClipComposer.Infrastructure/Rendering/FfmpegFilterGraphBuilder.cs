using System.Globalization;
using System.Text;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Infrastructure.Rendering;

internal static class FfmpegFilterGraphBuilder
{
    public static string Build(
        RenderRequest request,
        int width,
        int height,
        string? overlayTextPath,
        bool hasImageOverlay)
    {
        var graph = new StringBuilder();
        AddNormalizedSegments(graph, request, width, height);
        AddConcatenation(graph, request.Segments.Count);
        var currentLabel = "joinedv";
        var stage = 0;

        if (hasImageOverlay)
        {
            currentLabel = AddImageOverlay(graph, request, currentLabel, stage++);
        }

        if (!string.IsNullOrWhiteSpace(overlayTextPath))
        {
            currentLabel = AddTextOverlay(graph, request, overlayTextPath, currentLabel, stage++);
        }

        AddWholeCompilationProgressOrOutput(graph, request, width, currentLabel);
        return graph.ToString();
    }

    private static void AddNormalizedSegments(
        StringBuilder graph,
        RenderRequest request,
        int width,
        int height)
    {
        for (var index = 0; index < request.Segments.Count; index++)
        {
            var segment = request.Segments[index];
            var seconds = FormatSeconds(segment.Duration);
            var videoOutputLabel = request.ProgressStyle == VideoProgressStyle.EachClip
                ? $"basev{index}"
                : $"v{index}";
            graph.Append(CultureInfo.InvariantCulture, $"[{index}:v:0]");
            graph.Append(CultureInfo.InvariantCulture,
                $"scale={width}:{height}:force_original_aspect_ratio=decrease,");
            graph.Append(CultureInfo.InvariantCulture,
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,");
            graph.Append(CultureInfo.InvariantCulture,
                $"setsar=1,fps={request.FramesPerSecond},format={GetPixelFormat(request.VideoEncoder)},");
            graph.Append(CultureInfo.InvariantCulture,
                $"trim=duration={seconds},setpts=PTS-STARTPTS[{videoOutputLabel}];");

            if (request.ProgressStyle == VideoProgressStyle.EachClip)
            {
                AddProgressBar(graph, width, request.FramesPerSecond, seconds, index);
            }

            AddAudio(graph, segment, index, seconds);
        }
    }

    private static void AddProgressBar(
        StringBuilder graph,
        int width,
        int framesPerSecond,
        string seconds,
        int index)
    {
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c=0x7BD88F@0.92:s={width}x12:r={framesPerSecond}:d={seconds}[bar{index}];");
        graph.Append(CultureInfo.InvariantCulture,
            $"[basev{index}][bar{index}]overlay=x='-W+W*min(t/{seconds},1)':y=H-h:shortest=1[v{index}];");
    }

    private static void AddAudio(StringBuilder graph, RenderSegment segment, int index, string seconds)
    {
        if (segment.HasAudio)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{index}:a:0]");
            graph.Append("aresample=48000,");
            graph.Append("aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,");
            graph.Append(CultureInfo.InvariantCulture, $"atrim=duration={seconds},");
            graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
            return;
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"anullsrc=r=48000:cl=stereo,atrim=duration={seconds},");
        graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
    }

    private static void AddConcatenation(StringBuilder graph, int segmentCount)
    {
        for (var index = 0; index < segmentCount; index++)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[v{index}][a{index}]");
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"concat=n={segmentCount}:v=1:a=1[joinedv][outa];");
    }

    private static string AddImageOverlay(
        StringBuilder graph,
        RenderRequest request,
        string currentLabel,
        int stage)
    {
        var overlayInputIndex = request.Segments.Count;
        graph.Append(CultureInfo.InvariantCulture,
            $"[{overlayInputIndex}:v:0]scale='min(360,iw)':-2,format=rgba,colorchannelmixer=aa=0.85[watermark];");
        var (x, y) = GetImageOverlayCoordinates(request.OverlayPosition);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][watermark]overlay=x={x}:y={y}:shortest=1[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddTextOverlay(
        StringBuilder graph,
        RenderRequest request,
        string overlayTextPath,
        string currentLabel,
        int stage)
    {
        var (x, y) = GetTextOverlayCoordinates(request.OverlayPosition);
        var fontOption = string.IsNullOrWhiteSpace(request.OverlayFontPath)
            ? "font='Segoe UI':"
            : $"fontfile='{EscapeFilterValue(request.OverlayFontPath)}':";
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}]drawtext=textfile='{EscapeFilterValue(overlayTextPath)}':{fontOption}");
        graph.Append(CultureInfo.InvariantCulture,
            $"fontcolor=white:fontsize={Math.Clamp(request.OverlayTextSize, 8, 200)}:");
        graph.Append(CultureInfo.InvariantCulture,
            $"borderw=3:bordercolor=black@0.7:x={x}:y={y}[stage{stage}];");
        return $"stage{stage}";
    }

    private static void AddWholeCompilationProgressOrOutput(
        StringBuilder graph,
        RenderRequest request,
        int width,
        string currentLabel)
    {
        if (request.ProgressStyle != VideoProgressStyle.WholeCompilation)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{currentLabel}]null[outv]");
            return;
        }

        var totalSeconds = request.Segments.Sum(segment => segment.Duration.TotalSeconds)
            .ToString("0.######", CultureInfo.InvariantCulture);
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c=0x7BD88F@0.92:s={width}x12:r={request.FramesPerSecond}:d={totalSeconds}[wholebar];");
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][wholebar]overlay=x='-W+W*min(t/{totalSeconds},1)':y=H-h:shortest=1[outv]");
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

    private static string FormatSeconds(TimeSpan duration) =>
        duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);

    private static string GetPixelFormat(VideoEncoderPreset preset) =>
        preset == VideoEncoderPreset.WindowsMediaFoundationH264 ? "nv12" : "yuv420p";

    private static string EscapeFilterValue(string value) => value
        .Replace("\\", "/", StringComparison.Ordinal)
        .Replace(":", "\\:", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal);
}
