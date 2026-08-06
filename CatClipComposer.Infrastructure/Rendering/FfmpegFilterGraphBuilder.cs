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
        string? legacyOverlayTextPath,
        IReadOnlyDictionary<int, string> timedTextPaths,
        bool hasLegacyImageOverlay)
    {
        var graph = new StringBuilder();
        AddNormalizedSegments(graph, request, width, height);
        AddConcatenation(graph, request.Segments.Count);
        var currentLabel = "joinedv";
        var stage = 0;
        var nextInputIndex = request.Segments.Count;

        if (hasLegacyImageOverlay)
        {
            currentLabel = AddImageOverlay(
                graph,
                request.OverlayPosition,
                nextInputIndex++,
                currentLabel,
                stage++,
                null,
                null);
        }

        if (!string.IsNullOrWhiteSpace(legacyOverlayTextPath))
        {
            currentLabel = AddTextOverlay(
                graph,
                legacyOverlayTextPath,
                request.OverlayFontPath,
                request.OverlayTextSize,
                request.OverlayPosition,
                currentLabel,
                stage++,
                null,
                null);
        }

        var overlays = request.TimedOverlays ?? [];
        for (var index = 0; index < overlays.Count; index++)
        {
            var overlay = overlays[index];
            if (overlay.Duration <= TimeSpan.Zero)
            {
                continue;
            }

            switch (overlay.Kind)
            {
                case RenderOverlayKind.Text when timedTextPaths.TryGetValue(index, out var textPath):
                    currentLabel = AddTextOverlay(
                        graph,
                        textPath,
                        overlay.FontPath,
                        overlay.FontSize,
                        overlay.Position,
                        currentLabel,
                        stage++,
                        overlay.Start,
                        overlay.Duration);
                    break;
                case RenderOverlayKind.Image:
                    currentLabel = AddImageOverlay(
                        graph,
                        overlay.Position,
                        nextInputIndex++,
                        currentLabel,
                        stage++,
                        overlay.Start,
                        overlay.Duration);
                    break;
                case RenderOverlayKind.ProgressBar:
                    currentLabel = AddTimedProgress(
                        graph,
                        request,
                        width,
                        currentLabel,
                        stage++,
                        overlay.Start,
                        overlay.Duration);
                    break;
            }
        }

        AddWholeCompilationProgressOrOutput(graph, request, width, currentLabel);
        AddMixedAudio(graph, request, nextInputIndex);
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
            AddNormalizedVideo(
                graph,
                request,
                segment,
                index,
                width,
                height,
                seconds,
                videoOutputLabel);

            if (request.ProgressStyle == VideoProgressStyle.EachClip)
            {
                AddProgressBar(graph, width, request.FramesPerSecond, seconds, index);
            }

            AddAudio(graph, segment, index, seconds);
        }
    }

    private static void AddNormalizedVideo(
        StringBuilder graph,
        RenderRequest request,
        RenderSegment segment,
        int index,
        int width,
        int height,
        string seconds,
        string outputLabel)
    {
        var fps = FormatNumber(request.FramesPerSecond);
        var common = $"trim=duration={seconds},setpts=PTS-STARTPTS,fps={fps}," +
                     $"format={GetPixelFormat(request.VideoEncoder)},setsar=1";
        if (segment.FitMode == VideoFitMode.BlurBackground)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{index}:v:0]{common}[raw{index}];");
            graph.Append(CultureInfo.InvariantCulture, $"[raw{index}]split=2[bg{index}][fg{index}];");
            graph.Append(CultureInfo.InvariantCulture,
                $"[bg{index}]scale={width}:{height}:force_original_aspect_ratio=increase," +
                $"crop={width}:{height},gblur=sigma=32[back{index}];");
            graph.Append(CultureInfo.InvariantCulture,
                $"[fg{index}]scale={width}:{height}:force_original_aspect_ratio=decrease[front{index}];");
            graph.Append(CultureInfo.InvariantCulture,
                $"[back{index}][front{index}]overlay=(W-w)/2:(H-h)/2:shortest=1[composed{index}];");
            graph.Append(CultureInfo.InvariantCulture, $"[composed{index}]");
        }
        else
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{index}:v:0]");
            graph.Append(segment.FitMode switch
            {
                VideoFitMode.Fill =>
                    $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},",
                VideoFitMode.Stretch => $"scale={width}:{height},",
                _ =>
                    $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,"
            });
            graph.Append(common);
            graph.Append(',');
        }

        AddFadeFilters(graph, segment.FadeInSeconds, segment.FadeOutSeconds, segment.Duration);
        graph.Append(CultureInfo.InvariantCulture, $"null[{outputLabel}];");
    }

    private static void AddFadeFilters(
        StringBuilder graph,
        double fadeInSeconds,
        double fadeOutSeconds,
        TimeSpan duration)
    {
        var fadeIn = Math.Clamp(fadeInSeconds, 0, duration.TotalSeconds);
        var fadeOut = Math.Clamp(fadeOutSeconds, 0, duration.TotalSeconds);
        if (fadeIn > 0)
        {
            graph.Append(CultureInfo.InvariantCulture,
                $"fade=t=in:st=0:d={FormatNumber(fadeIn)},");
        }

        if (fadeOut > 0)
        {
            var start = Math.Max(0, duration.TotalSeconds - fadeOut);
            graph.Append(CultureInfo.InvariantCulture,
                $"fade=t=out:st={FormatNumber(start)}:d={FormatNumber(fadeOut)},");
        }
    }

    private static void AddProgressBar(
        StringBuilder graph,
        int width,
        double framesPerSecond,
        string seconds,
        int index)
    {
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c=0xC8C0B2@0.92:s={width}x10:r={FormatNumber(framesPerSecond)}:d={seconds}[bar{index}];");
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
            graph.Append(CultureInfo.InvariantCulture,
                $"volume={FormatNumber(Math.Clamp(segment.Volume, 0, 4))},");
            AddAudioFadeFilters(
                graph,
                segment.FadeInSeconds,
                segment.FadeOutSeconds,
                segment.Duration);
            graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
            return;
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"anullsrc=r=48000:cl=stereo,atrim=duration={seconds},");
        graph.Append(CultureInfo.InvariantCulture, $"asetpts=PTS-STARTPTS[a{index}];");
    }

    private static void AddAudioFadeFilters(
        StringBuilder graph,
        double fadeInSeconds,
        double fadeOutSeconds,
        TimeSpan duration)
    {
        var fadeIn = Math.Clamp(fadeInSeconds, 0, duration.TotalSeconds);
        var fadeOut = Math.Clamp(fadeOutSeconds, 0, duration.TotalSeconds);
        if (fadeIn > 0)
        {
            graph.Append(CultureInfo.InvariantCulture,
                $"afade=t=in:st=0:d={FormatNumber(fadeIn)},");
        }

        if (fadeOut > 0)
        {
            var start = Math.Max(0, duration.TotalSeconds - fadeOut);
            graph.Append(CultureInfo.InvariantCulture,
                $"afade=t=out:st={FormatNumber(start)}:d={FormatNumber(fadeOut)},");
        }
    }

    private static void AddConcatenation(StringBuilder graph, int segmentCount)
    {
        for (var index = 0; index < segmentCount; index++)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[v{index}][a{index}]");
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"concat=n={segmentCount}:v=1:a=1[joinedv][joineda];");
    }

    private static string AddImageOverlay(
        StringBuilder graph,
        OverlayPosition position,
        int inputIndex,
        string currentLabel,
        int stage,
        TimeSpan? start,
        TimeSpan? duration)
    {
        graph.Append(CultureInfo.InvariantCulture,
            $"[{inputIndex}:v:0]scale='min(480,iw)':-2,format=rgba,colorchannelmixer=aa=0.9[layerimage{stage}];");
        var (x, y) = GetImageOverlayCoordinates(position);
        var enable = CreateEnable(start, duration);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][layerimage{stage}]overlay=x={x}:y={y}:eof_action=repeat:shortest=1{enable}[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddTextOverlay(
        StringBuilder graph,
        string overlayTextPath,
        string? fontPath,
        int fontSize,
        OverlayPosition position,
        string currentLabel,
        int stage,
        TimeSpan? start,
        TimeSpan? duration)
    {
        var (x, y) = GetTextOverlayCoordinates(position);
        var fontOption = string.IsNullOrWhiteSpace(fontPath)
            ? "font='Segoe UI':"
            : $"fontfile='{EscapeFilterValue(fontPath)}':";
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}]drawtext=textfile='{EscapeFilterValue(overlayTextPath)}':{fontOption}");
        graph.Append(CultureInfo.InvariantCulture,
            $"fontcolor=white:fontsize={Math.Clamp(fontSize, 8, 240)}:");
        graph.Append(CultureInfo.InvariantCulture,
            $"borderw=3:bordercolor=black@0.72:x={x}:y={y}{CreateEnable(start, duration)}[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddTimedProgress(
        StringBuilder graph,
        RenderRequest request,
        int width,
        string currentLabel,
        int stage,
        TimeSpan start,
        TimeSpan duration)
    {
        var startSeconds = FormatSeconds(start);
        var seconds = FormatSeconds(duration);
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c=0xC8C0B2@0.92:s={width}x10:r={FormatNumber(request.FramesPerSecond)}:" +
            $"d={seconds},setpts=PTS+{startSeconds}/TB[timedbar{stage}];");
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][timedbar{stage}]overlay=" +
            $"x='-W+W*min(max((t-{startSeconds})/{seconds},0),1)':y=H-h:" +
            $"enable='between(t,{startSeconds},{FormatSeconds(start + duration)})'[stage{stage}];");
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
            graph.Append(CultureInfo.InvariantCulture, $"[{currentLabel}]null[outv];");
            return;
        }

        var totalSeconds = request.Segments.Sum(segment => segment.Duration.TotalSeconds);
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c=0xC8C0B2@0.92:s={width}x10:r={FormatNumber(request.FramesPerSecond)}:" +
            $"d={FormatNumber(totalSeconds)}[wholebar];");
        graph.Append(
            $"[{currentLabel}][wholebar]overlay=x='-W+W*min(t/{FormatNumber(totalSeconds)},1)':" +
            "y=H-h:shortest=1[outv];");
    }

    private static void AddMixedAudio(
        StringBuilder graph,
        RenderRequest request,
        int firstAudioInputIndex)
    {
        var layers = request.AudioLayers ?? [];
        if (layers.Count == 0)
        {
            graph.Append("[joineda]anull[outa]");
            return;
        }

        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            var seconds = FormatSeconds(layer.Duration);
            graph.Append(
                $"[{firstAudioInputIndex + index}:a:0]aresample=48000," +
                "aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo," +
                $"atrim=duration={seconds},asetpts=PTS-STARTPTS," +
                $"volume={FormatNumber(Math.Clamp(layer.Volume, 0, 4))},");
            AddAudioFadeFilters(
                graph,
                layer.FadeInSeconds,
                layer.FadeOutSeconds,
                layer.Duration);
            var delay = Math.Max(0, (long)Math.Round(layer.Start.TotalMilliseconds));
            graph.Append(CultureInfo.InvariantCulture,
                $"adelay={delay}|{delay}[music{index}];");
        }

        graph.Append("[joineda]");
        for (var index = 0; index < layers.Count; index++)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[music{index}]");
        }

        graph.Append(CultureInfo.InvariantCulture,
            $"amix=inputs={layers.Count + 1}:duration=first:dropout_transition=2:normalize=0[outa]");
    }

    private static string CreateEnable(TimeSpan? start, TimeSpan? duration) =>
        start.HasValue && duration.HasValue
            ? $":enable='between(t,{FormatSeconds(start.Value)},{FormatSeconds(start.Value + duration.Value)})'"
            : string.Empty;

    private static (string X, string Y) GetImageOverlayCoordinates(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => ("28", "28"),
        OverlayPosition.TopRight => ("W-w-28", "28"),
        OverlayPosition.BottomLeft => ("28", "H-h-28"),
        OverlayPosition.BottomRight => ("W-w-28", "H-h-28"),
        _ => ("(W-w)/2", "(H-h)/2")
    };

    private static (string X, string Y) GetTextOverlayCoordinates(OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft => ("28", "28"),
        OverlayPosition.TopRight => ("w-text_w-28", "28"),
        OverlayPosition.BottomLeft => ("28", "h-text_h-28"),
        OverlayPosition.BottomRight => ("w-text_w-28", "h-text_h-28"),
        _ => ("(w-text_w)/2", "(h-text_h)/2")
    };

    private static string FormatSeconds(TimeSpan duration) => FormatNumber(duration.TotalSeconds);

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

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
