using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public sealed record ProjectRenderPlan(
    IReadOnlyList<RenderSegment> Segments,
    IReadOnlyList<RenderOverlay> TimedOverlays,
    IReadOnlyList<RenderAudioLayer> AudioLayers);

public static class ProjectRenderMapper
{
    public static ProjectRenderPlan Create(EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var enabledTracks = project.Tracks.Where(track => track.IsEnabled).ToList();
        var segments = enabledTracks
            .Where(track => track.Kind == ProjectTrackKind.Video)
            .SelectMany(track => track.Items)
            .Where(item => item.IsEnabled && item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage)
            .OrderBy(item => item.StartTicks)
            .Select(item => new RenderSegment(
                item.Kind == ProjectItemKind.StillImage
                    ? RenderSegmentKind.StillImage
                    : RenderSegmentKind.Video,
                item.SourcePath,
                item.Duration,
                item.HasAudio,
                item.MediaFileId,
                item.FitMode,
                item.FadeInSeconds,
                item.FadeOutSeconds,
                item.Volume))
            .ToList();

        var overlays = new List<RenderOverlay>();
        foreach (var track in enabledTracks)
        {
            foreach (var item in track.Items.Where(item => item.IsEnabled))
            {
                if (track.Kind == ProjectTrackKind.Overlay && item.Kind == ProjectItemKind.TextOverlay)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.Text,
                        item.Start,
                        item.Duration,
                        Text: item.Text,
                        FontPath: item.FontPath,
                        FontFamily: item.FontFamily,
                        FontSize: item.FontSize,
                        Position: item.Position));
                }
                else if (track.Kind == ProjectTrackKind.Overlay && item.Kind == ProjectItemKind.ImageOverlay)
                {
                    overlays.Add(new RenderOverlay(
                        RenderOverlayKind.Image,
                        item.Start,
                        item.Duration,
                        SourcePath: item.SourcePath,
                        Position: item.Position));
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
                        ProgressHeight: item.ProgressHeight));
                }
            }
        }

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

        return new ProjectRenderPlan(
            segments,
            overlays.OrderBy(item => item.Start).ToList(),
            audioLayers);
    }
}
