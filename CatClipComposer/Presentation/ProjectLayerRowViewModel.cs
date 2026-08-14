using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class ProjectLayerRowViewModel : ObservableObject
{
    private bool _isSelected;

    private ProjectLayerRowViewModel(
        ProjectTrack track,
        ProjectTimelineItem? item,
        string title,
        string detail)
    {
        Track = track;
        Item = item;
        Title = title;
        Detail = detail;
    }

    public ProjectTrack Track { get; }

    public ProjectTimelineItem? Item { get; }

    public bool IsTrackHeader => Item is null;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Title { get; }

    public string Detail { get; }

    public string Color => !string.IsNullOrWhiteSpace(Item?.Color)
        ? Item.Color
        : !string.IsNullOrWhiteSpace(Track.Color)
            ? Track.Color
            : IsTrackHeader ? "#24231F" : "Transparent";

    public bool IsEnabled => Item?.IsEnabled ?? Track.IsEnabled;

    public string EnableActionText => IsEnabled ? "Disable item" : "Enable item";

    public bool IsPositionableOverlay => Item?.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or
        ProjectItemKind.VideoOverlay;

    public string TransformLockActionText => Item?.IsTransformLocked == true
        ? "Unlock overlay transform"
        : "Lock overlay transform";

    public double ContentOpacity => IsEnabled ? 1 : 0.46;

    public static ProjectLayerRowViewModel ForTrack(ProjectTrack track) => new(
        track,
        null,
        track.Name.ToUpperInvariant(),
        $"{track.Items.Count} item(s){(track.IsEnabled ? string.Empty : " | disabled")}");

    public static ProjectLayerRowViewModel ForItem(ProjectTrack track, ProjectTimelineItem item)
    {
        var details = item.Kind switch
        {
            ProjectItemKind.Video or ProjectItemKind.StillImage =>
                $" | {item.FitMode} | fade {item.FadeInSeconds:0.##}/{item.FadeOutSeconds:0.##}s",
            ProjectItemKind.TextOverlay =>
                $" | {item.FontFamily}{(string.IsNullOrWhiteSpace(item.FontPath) ? string.Empty : " | CUSTOM")}" +
                DescribeOverlayTransform(item),
            ProjectItemKind.ImageOverlay => DescribeOverlayTransform(item),
            ProjectItemKind.VideoOverlay => $" | moving media{DescribeOverlayTransform(item)}",
            ProjectItemKind.Audio => $" | volume {item.Volume:0.##}",
            ProjectItemKind.ProgressBar =>
                $" | {item.ProgressBarStyle} | {item.ProgressColor} | {item.ProgressHeight}px | {item.ProgressBarPosition}",
            ProjectItemKind.Effect => $" | plugin {item.PluginId}",
            _ => string.Empty
        };
        return new ProjectLayerRowViewModel(
            track,
            item,
            item.Name,
            $"{DurationFormatter.Format(item.Start)} -> {DurationFormatter.Format(item.Start + item.Duration)} | " +
            $"{item.Kind}{details}{(item.IsEnabled ? string.Empty : " | DISABLED")}");
    }

    private static string DescribeOverlayTransform(ProjectTimelineItem item) => item.HasCustomOverlayTransform
        ? $" | {(item.IsTransformLocked ? "LOCKED | " : string.Empty)}X {item.OverlayX * 100:0.#}% | Y {item.OverlayY * 100:0.#}% | " +
          $"scale {item.OverlayScale * 100:0.#}% | rotate {item.OverlayRotationDegrees:0.#}° | opacity {item.OverlayOpacity * 100:0.#}%"
        : $" | {(item.IsTransformLocked ? "LOCKED | " : string.Empty)}{item.Position} | opacity {item.OverlayOpacity * 100:0.#}%";
}
