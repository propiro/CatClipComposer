using CatClipComposer.Core.Models;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Core.Services;

public sealed record ProjectRenderPlan(
    IReadOnlyList<RenderSegment> Segments,
    IReadOnlyList<RenderOverlay> TimedOverlays,
    IReadOnlyList<RenderAudioLayer> AudioLayers,
    IReadOnlyList<RenderPluginEffect> PluginEffects);

public static class ProjectRenderMapper
{
    public static ProjectRenderPlan Create(EditorProject project, IPluginCatalog plugins)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plugins);
        var enabledTracks = project.Tracks.Where(track => track.IsEnabled).ToList();
        var primaryVideoTrack = enabledTracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .OrderByDescending(track => track.Order)
            .FirstOrDefault();
        var backgroundItems = enabledTracks
            .Where(track => track.Kind == ProjectTrackKind.Background)
            .OrderByDescending(track => track.Order)
            .SelectMany(track => track.Items)
            .Where(item => item.IsEnabled && item.Kind == ProjectItemKind.Effect)
            .OrderBy(item => item.StartTicks)
            .ToList();
        var segments = (primaryVideoTrack?.Items ?? [])
            .Where(item => item.IsEnabled && item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
            .OrderBy(item => item.StartTicks)
            .Select(item => CreateSegment(item, backgroundItems, plugins))
            .ToList();

        var overlays = new List<RenderOverlay>();
        foreach (var track in enabledTracks.OrderByDescending(track => track.Order))
        {
            foreach (var item in track.Items.Where(item => item.IsEnabled))
            {
                if (track.Kind == ProjectTrackKind.Video &&
                    track.Id != primaryVideoTrack?.Id &&
                    item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                {
                    overlays.Add(new RenderOverlay(
                        item.Kind == ProjectItemKind.StillImage ? RenderOverlayKind.Image : RenderOverlayKind.Video,
                        item.Start,
                        item.Duration,
                        SourcePath: item.SourcePath,
                        FitMode: item.FitMode,
                        TrackOrder: track.Order,
                        ProjectItemId: item.Id));
                }
                else if (track.Kind == ProjectTrackKind.Overlay && item.Kind == ProjectItemKind.TextOverlay)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.Text,
                        item.Start,
                        item.Duration,
                        Text: item.Text,
                        FontPath: item.FontPath,
                        FontFamily: item.FontFamily,
                        FontSize: item.FontSize,
                        TextStrokeEnabled: item.TextStrokeEnabled,
                        TextStrokeColor: item.TextStrokeColor,
                        TextStrokeWidth: item.TextStrokeWidth,
                        TextStrokeSmoothness: item.TextStrokeSmoothness,
                        Position: item.Position,
                        TrackOrder: track.Order,
                        HasCustomTransform: item.HasCustomOverlayTransform,
                        TransformX: item.OverlayX,
                        TransformY: item.OverlayY,
                        TransformScale: item.OverlayScale,
                        TransformRotationDegrees: item.OverlayRotationDegrees,
                        Opacity: item.OverlayOpacity,
                        FadeInSeconds: item.FadeInSeconds,
                        FadeOutSeconds: item.FadeOutSeconds,
                        ProjectItemId: item.Id));
                }
                else if (track.Kind == ProjectTrackKind.Overlay && item.Kind == ProjectItemKind.ImageOverlay)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.Image,
                        item.Start,
                        item.Duration,
                        SourcePath: item.SourcePath,
                        Position: item.Position,
                        TrackOrder: track.Order,
                        HasCustomTransform: item.HasCustomOverlayTransform,
                        TransformX: item.OverlayX,
                        TransformY: item.OverlayY,
                        TransformScale: item.OverlayScale,
                        TransformRotationDegrees: item.OverlayRotationDegrees,
                        Opacity: item.OverlayOpacity,
                        FadeInSeconds: item.FadeInSeconds,
                        FadeOutSeconds: item.FadeOutSeconds,
                        ProjectItemId: item.Id));
                }
                else if (track.Kind == ProjectTrackKind.Overlay && item.Kind == ProjectItemKind.VideoOverlay)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.Video,
                        item.Start,
                        item.Duration,
                        SourcePath: item.SourcePath,
                        Position: item.Position,
                        TrackOrder: track.Order,
                        HasCustomTransform: item.HasCustomOverlayTransform,
                        TransformX: item.OverlayX,
                        TransformY: item.OverlayY,
                        TransformScale: item.OverlayScale,
                        TransformRotationDegrees: item.OverlayRotationDegrees,
                        Opacity: item.OverlayOpacity,
                        FadeInSeconds: item.FadeInSeconds,
                        FadeOutSeconds: item.FadeOutSeconds,
                        ProjectItemId: item.Id));
                }
                else if (track.Kind == ProjectTrackKind.Progress && item.Kind == ProjectItemKind.ProgressBar)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.ProgressBar,
                        item.Start,
                        item.Duration,
                        ProgressBarStyle: item.ProgressBarStyle,
                        ProgressBarPosition: item.ProgressBarPosition,
                        ProgressColor: item.ProgressColor,
                        ProgressHeight: item.ProgressHeight,
                        TrackOrder: track.Order,
                        ProjectItemId: item.Id));
                }
            }
        }

        var secondaryVideoItems = enabledTracks
                     .Where(track => track.Kind == ProjectTrackKind.Video && track.Id != primaryVideoTrack?.Id)
                     .OrderByDescending(track => track.Order)
                     .SelectMany(track => track.Items)
                     .Where(item => item.IsEnabled && item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
                     .ToList();

        var audioLayers = enabledTracks
            .Where(track => track.Kind == ProjectTrackKind.Audio)
            .SelectMany(track => track.Items)
            .Where(item => item.IsEnabled && item.Kind == ProjectItemKind.Audio)
            .OrderBy(item => item.StartTicks)
            .Select(item => new RenderAudioLayer(
                item.SourcePath,
                item.Start,
                item.Duration,
                item.Volume,
                item.FadeInSeconds,
                item.FadeOutSeconds))
            .ToList();
        audioLayers.AddRange(secondaryVideoItems
            .Where(item => item.Kind == ProjectItemKind.Video && item.HasAudio)
            .Select(item => new RenderAudioLayer(
                item.SourcePath,
                item.Start,
                item.Duration,
                item.Volume,
                item.FadeInSeconds,
                item.FadeOutSeconds)));

        var pluginEffects = enabledTracks
            .Where(track => track.Kind != ProjectTrackKind.Background)
            .OrderByDescending(track => track.Order)
            .SelectMany(track => track.Items.Select(item => (Track: track, Item: item)))
            .Where(entry => entry.Item.IsEnabled && entry.Item.Kind == ProjectItemKind.Effect)
            .Select(entry => CreateVideoEffect(entry.Item, plugins, entry.Track.Kind, entry.Track.Order))
            .ToList();

        return new ProjectRenderPlan(
            segments,
            overlays,
            audioLayers,
            pluginEffects);
    }

    private static RenderSegment CreateSegment(
        ProjectTimelineItem item,
        IReadOnlyList<ProjectTimelineItem> backgroundItems,
        IPluginCatalog plugins)
    {
        var kind = item.Kind == ProjectItemKind.StillImage
            ? RenderSegmentKind.StillImage
            : RenderSegmentKind.Video;
        if (!string.IsNullOrWhiteSpace(item.PluginId))
        {
            var sourcePlugin = plugins.Find(item.PluginId) as ICatClipSourcePlugin ??
                               throw new InvalidOperationException(
                                   $"Required source plugin '{item.PluginId}' is not loaded.");
            kind = sourcePlugin.ResolveSourceKind(item.SourcePath);
        }

        var backgroundItem = backgroundItems.LastOrDefault(candidate =>
            candidate.Start < item.Start + item.Duration &&
            candidate.Start + candidate.Duration > item.Start);
        RenderPluginEffect? backgroundEffect = null;
        if (backgroundItem is not null)
        {
            backgroundEffect = CreateVideoEffect(backgroundItem, plugins, ProjectTrackKind.Background);
        }

        return new RenderSegment(
            kind,
            item.SourcePath,
            item.Duration,
            item.HasAudio,
            item.MediaFileId,
            item.FitMode,
            item.FadeInSeconds,
            item.FadeOutSeconds,
            item.Volume,
            item.Start,
            backgroundEffect);
    }

    private static RenderPluginEffect CreateVideoEffect(
        ProjectTimelineItem item,
        IPluginCatalog plugins,
        ProjectTrackKind trackKind,
        int trackOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(item.PluginId) ||
            plugins.Find(item.PluginId) is not ICatClipVideoEffectPlugin plugin)
        {
            throw new InvalidOperationException(
                $"Required video plugin '{item.PluginId}' is not loaded or does not provide video filtering.");
        }

        if (!plugin.Descriptor.CompatibleTracks.Contains(trackKind))
        {
            throw new InvalidOperationException(
                $"Plugin '{plugin.Descriptor.Id}' is not compatible with a {trackKind} timeline.");
        }

        return new RenderPluginEffect(
            plugin,
            item.Start,
            item.Duration,
            new Dictionary<string, string>(item.PluginParameters, StringComparer.OrdinalIgnoreCase),
            trackOrder,
            item.Id);
    }
}
