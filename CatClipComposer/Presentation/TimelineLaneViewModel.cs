using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class TimelineLaneViewModel
{
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
}

public sealed class TimelineLaneItemViewModel
{
    public TimelineLaneItemViewModel(
        ProjectTrack track,
        ProjectTimelineItem item,
        TimelineClipViewModel? clip,
        double pixelsPerSecond,
        double trackHeight,
        bool isSelected)
    {
        Id = item.Id;
        TrackKind = track.Kind;
        Kind = item.Kind;
        Title = item.Name;
        Detail = $"{DurationFormatter.Format(item.Start)} + {DurationFormatter.Format(item.Duration)}";
        ThumbnailPath = clip?.ThumbnailPath;
        Left = Math.Max(0, item.Start.TotalSeconds * pixelsPerSecond);
        Width = Math.Max(20, item.Duration.TotalSeconds * pixelsPerSecond);
        Height = Math.Max(22, trackHeight - 5);
        IsVideo = track.Kind == ProjectTrackKind.Video;
        IsSelected = isSelected;
        TrackId = track.Id;
        ShowClipActions = IsVideo && isSelected;
        Background = !string.IsNullOrWhiteSpace(item.Color)
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
    }

    public Guid Id { get; }

    public Guid TrackId { get; }

    public ProjectTrackKind TrackKind { get; }

    public ProjectItemKind Kind { get; }

    public string Title { get; }

    public string Detail { get; }

    public string? ThumbnailPath { get; }

    public double Left { get; }

    public double Width { get; }

    public double Height { get; }

    public bool IsVideo { get; }

    public bool IsSelected { get; }

    public bool ShowClipActions { get; }

    public string Background { get; }
}

public sealed record TimelineTickViewModel(
    double Left,
    double TickHeight,
    string Label);
