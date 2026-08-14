using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class TimelineLaneViewModel : ObservableObject
{
    private bool _isDropPreviewVisible;
    private double _dropPreviewLeft;
    private double _dropPreviewWidth;
    private string _dropPreviewText = string.Empty;

    public TimelineLaneViewModel(
        ProjectTrack track,
        int kindOrdinal,
        IEnumerable<TimelineLaneItemViewModel> items)
    {
        TrackId = track.Id;
        TrackKind = track.Kind;
        TrackColor = string.IsNullOrWhiteSpace(track.Color) ? "#1A1A18" : track.Color;
        Name = track.Name.ToUpperInvariant();
        ShortName = track.Kind switch
        {
            ProjectTrackKind.Background => $"BG{kindOrdinal}",
            ProjectTrackKind.Video => $"V{kindOrdinal}",
            ProjectTrackKind.Overlay => $"OV{kindOrdinal}",
            ProjectTrackKind.Audio => $"A{kindOrdinal}",
            ProjectTrackKind.Progress => $"PB{kindOrdinal}",
            _ => $"FX{kindOrdinal}"
        };
        Items = new ObservableCollection<TimelineLaneItemViewModel>(items);
    }

    public ProjectTrackKind TrackKind { get; }

    public Guid TrackId { get; }

    public string TrackColor { get; }

    public string Name { get; }

    public string ShortName { get; }

    public ObservableCollection<TimelineLaneItemViewModel> Items { get; }

    public bool IsDropPreviewVisible
    {
        get => _isDropPreviewVisible;
        private set => SetProperty(ref _isDropPreviewVisible, value);
    }

    public double DropPreviewLeft
    {
        get => _dropPreviewLeft;
        private set => SetProperty(ref _dropPreviewLeft, value);
    }

    public double DropPreviewWidth
    {
        get => _dropPreviewWidth;
        private set => SetProperty(ref _dropPreviewWidth, value);
    }

    public string DropPreviewText
    {
        get => _dropPreviewText;
        private set => SetProperty(ref _dropPreviewText, value);
    }

    public void ShowDropPreview(TimeSpan start, TimeSpan duration, double pixelsPerSecond)
    {
        DropPreviewLeft = Math.Max(0, start.TotalSeconds * pixelsPerSecond);
        DropPreviewWidth = Math.Max(20, duration.TotalSeconds * pixelsPerSecond);
        DropPreviewText = $"{DurationFormatter.Format(start)} – {DurationFormatter.Format(start + duration)}";
        IsDropPreviewVisible = true;
    }

    public void HideDropPreview() => IsDropPreviewVisible = false;
}

public sealed class TimelineLaneItemViewModel
{
    public TimelineLaneItemViewModel(
        ProjectTrack track,
        ProjectTimelineItem item,
        TimelineClipViewModel? clip,
        double pixelsPerSecond,
        double trackHeight,
        bool isSelected,
        bool needsProjectPreview,
        bool canResize)
    {
        Id = item.Id;
        TrackKind = track.Kind;
        Kind = item.Kind;
        Title = item.Name;
        SourcePath = item.SourcePath;
        Detail = $"{DurationFormatter.Format(item.Start)} – {DurationFormatter.Format(item.Start + item.Duration)}" +
                 (item.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or ProjectItemKind.VideoOverlay && item.HasCustomOverlayTransform
                     ? $" | {item.OverlayScale * 100:0.#}% / {item.OverlayRotationDegrees:0.#}°"
                     : string.Empty) +
                 (item.Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or ProjectItemKind.VideoOverlay &&
                  (item.FadeInSeconds > 0 || item.FadeOutSeconds > 0)
                     ? $" | fade {item.FadeInSeconds:0.##}/{item.FadeOutSeconds:0.##}s"
                     : string.Empty);
        ThumbnailPath = clip?.ThumbnailPath;
        Left = Math.Max(0, item.Start.TotalSeconds * pixelsPerSecond);
        Width = Math.Max(20, item.Duration.TotalSeconds * pixelsPerSecond);
        Height = Math.Max(22, trackHeight - 5);
        IsVideo = item.Kind is ProjectItemKind.Video or ProjectItemKind.StillImage;
        IsSelected = isSelected;
        TrackId = track.Id;
        Start = item.Start;
        Duration = item.Duration;
        ShowClipActions = IsVideo && isSelected;
        NeedsProjectPreview = needsProjectPreview;
        CanResize = canResize || item.Kind == ProjectItemKind.Effect;
        IsEnabled = item.IsEnabled;
        IsTransformLocked = item.IsTransformLocked;
        var enabledBackground = !string.IsNullOrWhiteSpace(item.Color)
            ? item.Color
            : !string.IsNullOrWhiteSpace(track.Color)
                ? track.Color
                : track.Kind switch
                {
                    ProjectTrackKind.Video => "#332F29",
                    ProjectTrackKind.Overlay => "#342F32",
                    ProjectTrackKind.Audio => "#2D332D",
                    ProjectTrackKind.Progress => "#39352A",
                    _ => "#302F2C"
                };
        Background = IsEnabled ? enabledBackground : "#171716";
    }

    public Guid Id { get; }

    public Guid TrackId { get; }

    public ProjectTrackKind TrackKind { get; }

    public TimeSpan Start { get; }

    public TimeSpan Duration { get; }

    public ProjectItemKind Kind { get; }

    public string Title { get; }

    public string? SourcePath { get; }

    public string Detail { get; }

    public string? ThumbnailPath { get; }

    public double Left { get; }

    public double Width { get; }

    public double Height { get; }

    public bool IsVideo { get; }

    public bool IsProgress => Kind == ProjectItemKind.ProgressBar;

    public bool IsPositionableOverlay => Kind is ProjectItemKind.TextOverlay or ProjectItemKind.ImageOverlay or
        ProjectItemKind.VideoOverlay;

    public bool IsTransformLocked { get; }

    public string TransformLockActionText => IsTransformLocked
        ? "Unlock overlay transform"
        : "Lock overlay transform";

    public bool IsSelected { get; }

    public bool ShowClipActions { get; }

    public bool NeedsProjectPreview { get; }

    public bool CanResize { get; }

    public bool IsEnabled { get; }

    public string EnableActionText => IsEnabled ? "Disable item" : "Enable item";

    public double ContentOpacity => IsEnabled ? 1 : 0.42;

    public string ToolTipText => NeedsProjectPreview
        ? $"{Title}\nNot included in the current Project Preview. Render preview to update it."
        : Title;

    public string Background { get; }
}

public sealed record TimelineTickViewModel(
    double Left,
    double TickHeight,
    string Label);

public sealed record TimelineItemMovePreview(
    TimeSpan Start,
    TimeSpan Duration);
