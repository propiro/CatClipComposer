using System.Collections.ObjectModel;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;

namespace CatClipComposer.Presentation;

public sealed class TimelineLaneViewModel
{
    public TimelineLaneViewModel(ProjectTrack track, IEnumerable<TimelineLaneItemViewModel> items)
    {
        TrackKind = track.Kind;
        Name = track.Name.ToUpperInvariant();
        ShortName = track.Kind switch
        {
            ProjectTrackKind.Video => "V1",
            ProjectTrackKind.Overlay => "OV",
            ProjectTrackKind.Audio => "A1",
            ProjectTrackKind.Progress => "PB",
            _ => "FX"
        };
        Items = new ObservableCollection<TimelineLaneItemViewModel>(items);
    }

    public ProjectTrackKind TrackKind { get; }

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
        Width = Math.Max(track.Kind == ProjectTrackKind.Video ? 112 : 54,
            item.Duration.TotalSeconds * pixelsPerSecond);
        Height = Math.Max(38, trackHeight - 5);
        IsVideo = track.Kind == ProjectTrackKind.Video;
        IsSelected = isSelected;
        ShowClipActions = IsVideo && isSelected;
        Background = track.Kind switch
        {
            ProjectTrackKind.Video => "#332F29",
            ProjectTrackKind.Overlay => "#342F32",
            ProjectTrackKind.Audio => "#2D332D",
            ProjectTrackKind.Progress => "#39352A",
            _ => "#302F2C"
        };
    }

    public Guid Id { get; }

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
