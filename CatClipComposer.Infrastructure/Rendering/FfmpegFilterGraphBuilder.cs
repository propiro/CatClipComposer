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
        var overlays = request.TimedOverlays ?? [];
        var overlayInputIndexes = Enumerable.Repeat(-1, overlays.Count).ToArray();
        var nextInputIndex = request.Segments.Count;
        for (var index = 0; index < overlays.Count; index++)
        {
            if (overlays[index].Kind is RenderOverlayKind.Image or RenderOverlayKind.Video)
            {
                overlayInputIndexes[index] = nextInputIndex++;
            }
        }

        var visualStages = overlays
            .Select((overlay, index) => new VisualStage(overlay.TrackOrder, index, overlay, null))
            .Concat((request.PluginEffects ?? [])
                .Where(effect => effect.Plugin.Descriptor.Stage is PluginRenderStage.Filter or PluginRenderStage.Overlay)
                .Select((effect, index) => new VisualStage(effect.TrackOrder, overlays.Count + index, null, effect)))
            .OrderByDescending(item => item.TrackOrder)
            .ThenBy(item => item.Sequence);
        foreach (var visualStage in visualStages)
        {
            if (visualStage.Effect is { } effect && effect.Duration > TimeSpan.Zero)
            {
                currentLabel = AddPluginEffect(graph, request, width, height, currentLabel, ref stage, effect);
                continue;
            }

            var overlay = visualStage.Overlay;
            if (overlay is null || overlay.Duration <= TimeSpan.Zero)
            {
                continue;
            }

            var index = visualStage.Sequence;
            switch (overlay.Kind)
            {
                case RenderOverlayKind.Text when timedTextPaths.TryGetValue(index, out var textPath):
                    currentLabel = AddTextOverlay(
                        graph,
                        request,
                        overlay,
                        textPath,
                        width,
                        height,
                        currentLabel,
                        stage++);
                    break;
                case RenderOverlayKind.Image:
                    currentLabel = AddImageOverlay(
                        graph,
                        request,
                        overlay,
                        overlayInputIndexes[index],
                        currentLabel,
                        stage++);
                    break;
                case RenderOverlayKind.Video:
                    currentLabel = AddVideoOverlay(
                        graph,
                        request,
                        overlay,
                        overlayInputIndexes[index],
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
                    request.BackgroundColor,
                    request.PreviewScale,
                    request.PreserveSelectedObjectQuality && effect.ProjectItemId == request.SelectedObjectId),
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

    private static string AddPluginEffect(
        StringBuilder graph,
        RenderRequest request,
        int width,
        int height,
        string currentLabel,
        ref int stage,
        RenderPluginEffect effect)
    {
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
                request.BackgroundColor,
                request.PreviewScale,
                request.PreserveSelectedObjectQuality && effect.ProjectItemId == request.SelectedObjectId),
            effect.Parameters));
        return outputLabel;
    }

    private sealed record VisualStage(
        int TrackOrder,
        int Sequence,
        RenderOverlay? Overlay,
        RenderPluginEffect? Effect);

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
        RenderRequest request,
        RenderOverlay overlay,
        int inputIndex,
        string currentLabel,
        int stage)
    {
        var overlayStart = overlay.Start;
        var overlayDuration = overlay.Duration;
        var scale = overlay.HasCustomTransform
            ? OverlayTransformValues.NormalizeScale(overlay.TransformScale)
            : 1;
        var previewScale = Math.Clamp(request.PreviewScale, 0.1, 1);
        var scaledWidth = FormatNumber(previewScale * scale);
        var scaler = request.PreserveSelectedObjectQuality && overlay.ProjectItemId == request.SelectedObjectId
            ? ":flags=lanczos"
            : ":flags=bilinear";
        graph.Append(
            $"[{inputIndex}:v:0]trim=duration={FormatSeconds(overlayDuration)}," +
            $"setpts=PTS-STARTPTS+{FormatSeconds(overlayStart)}/TB," +
            $"scale='max(2,min(480,iw)*{scaledWidth})':-2{scaler},setsar=1,format=yuva420p," +
            $"colorchannelmixer=aa={FormatNumber(Math.Clamp(overlay.Opacity, 0, 1))}");
        AppendOverlayAlphaFades(graph, overlay);
        AppendRotation(graph, overlay.TransformRotationDegrees, overlay.HasCustomTransform);
        graph.Append(CultureInfo.InvariantCulture, $"[layerimage{stage}];");
        var (x, y) = overlay.HasCustomTransform
            ? GetCustomOverlayCoordinates(overlay.TransformX, overlay.TransformY)
            : GetImageOverlayCoordinates(overlay.Position, request.PreviewScale);
        var enable = CreateEnable(overlay.Start, overlay.Duration);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][layerimage{stage}]overlay=x={x}:y={y}:" +
            $"eof_action=pass:repeatlast=0{enable}[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddVideoOverlay(
        StringBuilder graph,
        RenderRequest request,
        RenderOverlay overlay,
        int inputIndex,
        string currentLabel,
        int stage)
    {
        var scale = overlay.HasCustomTransform
            ? OverlayTransformValues.NormalizeScale(overlay.TransformScale)
            : 1;
        var previewScale = Math.Clamp(request.PreviewScale, 0.1, 1);
        var scaledWidth = FormatNumber(previewScale * scale);
        var scaler = request.PreserveSelectedObjectQuality && overlay.ProjectItemId == request.SelectedObjectId
            ? ":flags=lanczos"
            : ":flags=bilinear";
        graph.Append(
            $"[{inputIndex}:v:0]trim=duration={FormatSeconds(overlay.Duration)}," +
            $"setpts=PTS-STARTPTS+{FormatSeconds(overlay.Start)}/TB," +
            $"fps={FormatNumber(request.FramesPerSecond)}," +
            $"scale='max(2,min(480,iw)*{scaledWidth})':-2{scaler},setsar=1,format=yuva420p," +
            $"colorchannelmixer=aa={FormatNumber(Math.Clamp(overlay.Opacity, 0, 1))}");
        AppendOverlayAlphaFades(graph, overlay);
        AppendRotation(graph, overlay.TransformRotationDegrees, overlay.HasCustomTransform);
        graph.Append(CultureInfo.InvariantCulture, $"[layervideo{stage}];");
        var (x, y) = overlay.HasCustomTransform
            ? GetCustomOverlayCoordinates(overlay.TransformX, overlay.TransformY)
            : GetImageOverlayCoordinates(overlay.Position, request.PreviewScale);
        graph.Append(CultureInfo.InvariantCulture,
            $"[{currentLabel}][layervideo{stage}]overlay=x={x}:y={y}:" +
            $"eof_action=pass:repeatlast=0{CreateEnable(overlay.Start, overlay.Duration)}[stage{stage}];");
        return $"stage{stage}";
    }

    private static string AddTextOverlay(
        StringBuilder graph,
        RenderRequest request,
        RenderOverlay overlay,
        string overlayTextPath,
        int width,
        int height,
        string currentLabel,
        int stage)
    {
        var fontOption = string.IsNullOrWhiteSpace(overlay.FontPath)
            ? $"font='{EscapeFilterValue(string.IsNullOrWhiteSpace(overlay.FontFamily) ? "Segoe UI" : overlay.FontFamily)}':"
            : $"fontfile='{EscapeFilterValue(overlay.FontPath)}':";
        var opacity = Math.Clamp(overlay.Opacity, 0, 1);
        var previewScale = Math.Clamp(request.PreviewScale, 0.1, 1);
        var previewFontSize = Math.Clamp((int)Math.Round(overlay.FontSize * previewScale), 1, 2400);
        var strokeEnabled = overlay.TextStrokeEnabled && overlay.TextStrokeWidth > 0;
        var borderWidth = strokeEnabled
            ? Math.Max(1, (int)Math.Round(Math.Clamp(overlay.TextStrokeWidth, 0, 20) * previewScale))
            : 0;
        var strokeColor = NormalizeColor(overlay.TextStrokeColor);
        var strokeSmoothness = strokeEnabled
            ? Math.Clamp(overlay.TextStrokeSmoothness, 0, 10) * previewScale
            : 0;
        var hasFade = overlay.FadeInSeconds > 0 || overlay.FadeOutSeconds > 0;
        if (!overlay.HasCustomTransform && !hasFade && strokeSmoothness <= 0)
        {
            var (presetX, presetY) = GetTextOverlayCoordinates(overlay.Position, request.PreviewScale);
            graph.Append(CultureInfo.InvariantCulture,
                $"[{currentLabel}]drawtext=textfile='{EscapeFilterValue(overlayTextPath)}':{fontOption}");
            graph.Append(CultureInfo.InvariantCulture,
                $"text_shaping=0:fontcolor=white@{FormatNumber(opacity)}:fontsize={previewFontSize}:");
            graph.Append(CultureInfo.InvariantCulture,
                $"borderw={borderWidth}:bordercolor={strokeColor}@{FormatNumber(opacity)}:x={presetX}:y={presetY}" +
                $"{CreateEnable(overlay.Start, overlay.Duration)}[stage{stage}];");
            return $"stage{stage}";
        }

        var scale = overlay.HasCustomTransform
            ? OverlayTransformValues.NormalizeScale(overlay.TransformScale)
            : 1;
        var scaledFontSize = Math.Clamp((int)Math.Round(overlay.FontSize * scale * previewScale), 1, 2400);
        var duration = FormatSeconds(overlay.Duration);
        var start = FormatSeconds(overlay.Start);
        var (drawX, drawY) = overlay.HasCustomTransform
            ? ("(w-text_w)/2", "(h-text_h)/2")
            : GetTextOverlayCoordinates(overlay.Position, request.PreviewScale);
        var (x, y) = overlay.HasCustomTransform
            ? GetCustomOverlayCoordinates(overlay.TransformX, overlay.TransformY)
            : ("0", "0");

        var underlayLabel = currentLabel;
        if (strokeSmoothness > 0)
        {
            var softLabel = $"layertextsoft{stage}";
            AppendTextLayerSource(
                graph, request, overlay, overlayTextPath, fontOption, width, height, duration, start,
                scaledFontSize, drawX, drawY, borderWidth, strokeColor, opacity,
                softLabel, strokeSmoothness, strokeOnly: true);
            underlayLabel = $"textsoftstage{stage}";
            graph.Append(
                $"[{currentLabel}][{softLabel}]overlay=x={x}:y={y}:eof_action=pass:repeatlast=0" +
                $"{CreateEnable(overlay.Start, overlay.Duration)}[{underlayLabel}];");
        }

        var textLabel = $"layertext{stage}";
        AppendTextLayerSource(
            graph, request, overlay, overlayTextPath, fontOption, width, height, duration, start,
            scaledFontSize, drawX, drawY, borderWidth, strokeColor, opacity,
            textLabel, smoothness: 0, strokeOnly: false);
        graph.Append(
            $"[{underlayLabel}][{textLabel}]overlay=x={x}:y={y}:eof_action=pass:repeatlast=0" +
            $"{CreateEnable(overlay.Start, overlay.Duration)}[stage{stage}];");
        return $"stage{stage}";
    }

    private static void AppendTextLayerSource(
        StringBuilder graph,
        RenderRequest request,
        RenderOverlay overlay,
        string overlayTextPath,
        string fontOption,
        int width,
        int height,
        string duration,
        string start,
        int fontSize,
        string drawX,
        string drawY,
        int borderWidth,
        string strokeColor,
        double opacity,
        string outputLabel,
        double smoothness,
        bool strokeOnly)
    {
        var fill = strokeOnly ? $"{strokeColor}@0" : $"white@{FormatNumber(opacity)}";
        graph.Append(
            $"color=c=black@0.0:s={width}x{height}:r={FormatNumber(request.FramesPerSecond)}:d={duration}," +
            $"format=yuva420p,drawtext=textfile='{EscapeFilterValue(overlayTextPath)}':{fontOption}");
        graph.Append(CultureInfo.InvariantCulture,
            $"text_shaping=0:fontcolor={fill}:fontsize={fontSize}:borderw={borderWidth}:" +
            $"bordercolor={strokeColor}@{FormatNumber(opacity)}:x={drawX}:y={drawY}");
        AppendRotation(graph, overlay.TransformRotationDegrees, overlay.HasCustomTransform);
        graph.Append(CultureInfo.InvariantCulture, $",setpts=PTS+{start}/TB");
        AppendOverlayAlphaFades(graph, overlay);
        if (smoothness > 0)
        {
            graph.Append(CultureInfo.InvariantCulture,
                $",gblur=sigma={FormatNumber(smoothness)}:planes=8");
        }

        graph.Append(CultureInfo.InvariantCulture, $"[{outputLabel}];");
    }

    private static void AppendRotation(StringBuilder graph, double degrees, bool enabled)
    {
        var rotation = enabled ? OverlayTransformValues.NormalizeRotation(degrees) : 0;
        if (Math.Abs(rotation) < 0.000001)
        {
            return;
        }

        graph.Append(CultureInfo.InvariantCulture,
            $",rotate={FormatNumber(rotation)}*PI/180:ow=rotw(iw):oh=roth(ih):c=none");
    }

    private static void AppendOverlayAlphaFades(StringBuilder graph, RenderOverlay overlay)
    {
        var fadeIn = Math.Clamp(overlay.FadeInSeconds, 0, overlay.Duration.TotalSeconds);
        var fadeOut = Math.Clamp(overlay.FadeOutSeconds, 0, overlay.Duration.TotalSeconds);
        if (fadeIn > 0)
        {
            graph.Append(CultureInfo.InvariantCulture,
                $",fade=t=in:st={FormatSeconds(overlay.Start)}:d={FormatNumber(fadeIn)}:alpha=1");
        }

        if (fadeOut > 0)
        {
            var fadeOutStart = overlay.Start + overlay.Duration - TimeSpan.FromSeconds(fadeOut);
            graph.Append(CultureInfo.InvariantCulture,
                $",fade=t=out:st={FormatSeconds(fadeOutStart)}:d={FormatNumber(fadeOut)}:alpha=1");
        }
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
        var barHeight = Math.Clamp(
            (int)Math.Round(height * Math.Clamp(request.PreviewScale, 0.1, 1)),
            2,
            100);
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

    private static (string X, string Y) GetImageOverlayCoordinates(
        OverlayPosition position,
        double previewScale)
    {
        var margin = Math.Max(2, (int)Math.Round(28 * Math.Clamp(previewScale, 0.1, 1)));
        return position switch
        {
            OverlayPosition.TopLeft => ($"{margin}", $"{margin}"),
            OverlayPosition.TopRight => ($"W-w-{margin}", $"{margin}"),
            OverlayPosition.BottomLeft => ($"{margin}", $"H-h-{margin}"),
            OverlayPosition.BottomRight => ($"W-w-{margin}", $"H-h-{margin}"),
            _ => ("(W-w)/2", "(H-h)/2")
        };
    }

    private static (string X, string Y) GetTextOverlayCoordinates(
        OverlayPosition position,
        double previewScale)
    {
        var margin = Math.Max(2, (int)Math.Round(28 * Math.Clamp(previewScale, 0.1, 1)));
        return position switch
        {
            OverlayPosition.TopLeft => ($"{margin}", $"{margin}"),
            OverlayPosition.TopRight => ($"w-text_w-{margin}", $"{margin}"),
            OverlayPosition.BottomLeft => ($"{margin}", $"h-text_h-{margin}"),
            OverlayPosition.BottomRight => ($"w-text_w-{margin}", $"h-text_h-{margin}"),
            _ => ("(w-text_w)/2", "(h-text_h)/2")
        };
    }

    private static (string X, string Y) GetCustomOverlayCoordinates(double x, double y) =>
        ($"'W*{FormatNumber(OverlayTransformValues.NormalizeCoordinate(x))}-w/2'",
            $"'H*{FormatNumber(OverlayTransformValues.NormalizeCoordinate(y))}-h/2'");

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
