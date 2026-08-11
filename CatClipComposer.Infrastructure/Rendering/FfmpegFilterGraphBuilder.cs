using System.Globalization;
using System.Text;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Infrastructure.Rendering;

internal static class FfmpegFilterGraphBuilder
{
    public static string Build(
        RenderRequest request,
        int width,
        int height,
        IReadOnlyDictionary<int, string> timedTextPaths)
    {
        var graph = new StringBuilder();
        AddNormalizedSegments(graph, request, width, height);
        AddConcatenation(graph, request.Segments.Count);
        var currentLabel = "joinedv";
        var stage = 0;
        var nextInputIndex = request.Segments.Count;
        currentLabel = AddPluginEffects(
            graph,
            request,
            width,
            height,
            currentLabel,
            ref stage,
            PluginRenderStage.Filter);

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
                        overlay.FontFamily,
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
                case RenderOverlayKind.Video:
                    currentLabel = AddVideoOverlay(
                        graph,
                        request,
                        overlay,
                        width,
                        height,
                        nextInputIndex++,
                        currentLabel,
                        stage++);
                    break;
                case RenderOverlayKind.ProgressBar:
                    currentLabel = AddTimedProgress(
                        graph,
                        request,
                        width,
                        currentLabel,
                        stage++,
                        overlay.Start,
                        overlay.Duration,
                        overlay.ProgressBarStyle,
                        overlay.ProgressBarPosition,
                        overlay.ProgressColor,
                        overlay.ProgressHeight);
                    break;
            }
        }

        currentLabel = AddPluginEffects(
            graph,
            request,
            width,
            height,
            currentLabel,
            ref stage,
            PluginRenderStage.Overlay);

        // Scale and overlay filters can round a fitted portrait source one pixel beyond the canvas
        // (for example 1920x1081). Normalize the completed composition before it reaches an encoder;
        // Media Foundation H.264 rejects odd dimensions and plugins must not change project geometry.
        var normalizedCanvasLabel = $"canvas{stage++}";
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}]scale={width}:{height},setsar=1," +
            $"format={GetPixelFormat(request.VideoEncoder)}[{normalizedCanvasLabel}];");
        currentLabel = normalizedCanvasLabel;

        if (request.OutputRangeStart.HasValue && request.OutputRangeDuration.HasValue)
        {
            var start = FormatSeconds(request.OutputRangeStart.Value);
            var duration = FormatSeconds(request.OutputRangeDuration.Value);
            graph.Append(CultureInfo.InvariantCulture,
                $"[{currentLabel}]trim=start={start}:duration={duration},setpts=PTS-STARTPTS[outv];");
            AddMixedAudio(graph, request, nextInputIndex, "mixeda");
            graph.Append(CultureInfo.InvariantCulture,
                $";[mixeda]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS[outa]");
        }
        else
        {
            graph.Append(CultureInfo.InvariantCulture, $"[{currentLabel}]null[outv];");
            AddMixedAudio(graph, request, nextInputIndex, "outa");
        }

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
            AddNormalizedVideo(
                graph,
                request,
                segment,
                index,
                width,
                height,
                seconds,
                $"v{index}");

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
        graph.Append(CultureInfo.InvariantCulture, $"[{index}:v:0]{common}[raw{index}];");
        if (segment.BackgroundEffect is not null)
        {
            var effect = segment.BackgroundEffect;
            var relativeStart = effect.Start - segment.TimelineStart;
            var relativeEnd = effect.Start + effect.Duration - segment.TimelineStart;
            var clippedStart = relativeStart < TimeSpan.Zero ? TimeSpan.Zero : relativeStart;
            var clippedEnd = relativeEnd > segment.Duration ? segment.Duration : relativeEnd;
            var pluginOutput = $"background{index}";
            graph.Append(effect.Plugin.BuildFilterGraph(
                new PluginVideoFilterContext(
                    $"raw{index}",
                    pluginOutput,
                    width,
                    height,
                    request.FramesPerSecond,
                    clippedStart,
                    clippedEnd - clippedStart,
                    request.BackgroundColor),
                effect.Parameters));
            graph.Append(CultureInfo.InvariantCulture, $"[{pluginOutput}]");
        }
        else
        {
            graph.Append(CultureInfo.InvariantCulture, $"[raw{index}]");
            graph.Append(segment.FitMode switch
            {
                VideoFitMode.Fill =>
                    $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},",
                VideoFitMode.Stretch => $"scale={width}:{height},",
                _ =>
                    $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color={NormalizeColor(request.BackgroundColor)},"
            });
        }

        // Scale may recalculate SAR to preserve the input display aspect ratio. Concat requires every
        // normalized segment to have identical geometry *and* sample aspect ratio, so reset it after
        // all scale/pad/background-plugin work rather than relying on the earlier input normalization.
        // Background plugins can negotiate an alpha-capable intermediate format through their own
        // overlay filters. Normalize each completed segment before concat so a later image/video
        // overlay never has to negotiate against plugin-internal alpha state.
        graph.Append(CultureInfo.InvariantCulture,
            $"setsar=1,format={GetPixelFormat(request.VideoEncoder)},");
        AddFadeFilters(graph, segment.FadeInSeconds, segment.FadeOutSeconds, segment.Duration);
        graph.Append(CultureInfo.InvariantCulture, $"null[{outputLabel}];");
    }

    private static string AddPluginEffects(
        StringBuilder graph,
        RenderRequest request,
        int width,
        int height,
        string currentLabel,
        ref int stage,
        PluginRenderStage targetStage)
    {
        foreach (var effect in (request.PluginEffects ?? [])
                     .Where(effect => effect.Plugin.Descriptor.Stage == targetStage)
                     .OrderBy(effect => effect.Start))
        {
            if (effect.Duration <= TimeSpan.Zero)
            {
                continue;
            }

            var outputLabel = $"plugin{stage++}";
            graph.Append(effect.Plugin.BuildFilterGraph(
                new PluginVideoFilterContext(
                    currentLabel,
                    outputLabel,
                    width,
                    height,
                    request.FramesPerSecond,
                    effect.Start,
                    effect.Duration,
                    request.BackgroundColor),
                effect.Parameters));
            currentLabel = outputLabel;
        }

        return currentLabel;
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
        var overlayStart = start ?? TimeSpan.Zero;
        var overlayDuration = duration ?? TimeSpan.FromSeconds(5);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{inputIndex}:v:0]trim=duration={FormatSeconds(overlayDuration)}," +
            $"setpts=PTS-STARTPTS+{FormatSeconds(overlayStart)}/TB," +
            $"scale='min(480,iw)':-2,setsar=1,format=yuva420p," +
            $"colorchannelmixer=aa=0.9[layerimage{stage}];");
        var (x, y) = GetImageOverlayCoordinates(position);
        var enable = CreateEnable(start, duration);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][layerimage{stage}]overlay=x={x}:y={y}:" +
            $"eof_action=pass:repeatlast=0{enable}[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddVideoOverlay(
        StringBuilder graph,
        RenderRequest request,
        RenderOverlay overlay,
        int width,
        int height,
        int inputIndex,
        string currentLabel,
        int stage)
    {
        var seconds = FormatSeconds(overlay.Duration);
        var start = FormatSeconds(overlay.Start);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{inputIndex}:v:0]trim=duration={seconds},setpts=PTS-STARTPTS+{start}/TB," +
            $"fps={FormatNumber(request.FramesPerSecond)},format={GetPixelFormat(request.VideoEncoder)},setsar=1,");
        graph.Append(overlay.FitMode switch
        {
            VideoFitMode.Fill =>
                $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}",
            VideoFitMode.Stretch => $"scale={width}:{height}",
            _ =>
                $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color={NormalizeColor(request.BackgroundColor)}"
        });
        graph.Append(CultureInfo.InvariantCulture, $",setsar=1[layervideo{stage}];");
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][layervideo{stage}]overlay=0:0:eof_action=pass:repeatlast=0:" +
            $"enable='between(t,{start},{FormatSeconds(overlay.Start + overlay.Duration)})'[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddTextOverlay(
        StringBuilder graph,
        string overlayTextPath,
        string? fontPath,
        string? fontFamily,
        int fontSize,
        OverlayPosition position,
        string currentLabel,
        int stage,
        TimeSpan? start,
        TimeSpan? duration)
    {
        var (x, y) = GetTextOverlayCoordinates(position);
        var fontOption = string.IsNullOrWhiteSpace(fontPath)
            ? $"font='{EscapeFilterValue(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily)}':"
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
        TimeSpan duration,
        ProgressBarStyle style,
        ProgressBarPosition position,
        string color,
        int height)
    {
        var startSeconds = FormatSeconds(start);
        var seconds = FormatSeconds(duration);
        var barHeight = Math.Clamp(height, 2, 100);
        var barColor = NormalizeColor(color);
        var pattern = style switch
        {
            ProgressBarStyle.Segmented => $",drawgrid=w=80:h={barHeight}:t=3:c=black@0.75",
            ProgressBarStyle.Ticks => $",drawgrid=w=24:h={barHeight}:t=5:c=black@0.82",
            _ => string.Empty
        };
        var y = position == ProgressBarPosition.Top ? "0" : "H-h";
        graph.Append(CultureInfo.InvariantCulture,
            $"color=c={barColor}@0.92:s={width}x{barHeight}:r={FormatNumber(request.FramesPerSecond)}:" +
            $"d={seconds}{pattern},setpts=PTS+{startSeconds}/TB[timedbar{stage}];");
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][timedbar{stage}]overlay=" +
            $"x='-W+W*min(max((t-{startSeconds})/{seconds},0),1)':y={y}:" +
            $"enable='between(t,{startSeconds},{FormatSeconds(start + duration)})'[stage{stage}];");
        return $"stage{stage}";
    }

    private static void AddMixedAudio(
        StringBuilder graph,
        RenderRequest request,
        int firstAudioInputIndex,
        string outputLabel)
    {
        var layers = request.AudioLayers ?? [];
        if (layers.Count == 0)
        {
            graph.Append(CultureInfo.InvariantCulture, $"[joineda]anull[{outputLabel}]");
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
            $"amix=inputs={layers.Count + 1}:duration=first:dropout_transition=2:normalize=0[{outputLabel}]");
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

    private static string NormalizeColor(string color)
    {
        var hex = color.Trim().TrimStart('#');
        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? $"0x{hex}" : "0xC8C0B2";
    }

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
