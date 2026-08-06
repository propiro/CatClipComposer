using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;
using System.IO;

namespace CatClipComposer.Presentation;

public sealed class TimelineClipViewModel : ObservableObject
{
    private int _order;

    private TimelineClipViewModel(
        RenderSegmentKind kind,
        string sourcePath,
        TimeSpan duration,
        bool hasAudio,
        MediaFile? media,
        int order)
    {
        Kind = kind;
        SourcePath = sourcePath;
        Duration = duration;
        HasAudio = hasAudio;
        Media = media;
        _order = order;
    }

    public Guid InstanceId { get; } = Guid.NewGuid();

    public RenderSegmentKind Kind { get; }

    public string SourcePath { get; }

    public TimeSpan Duration { get; }

    public bool HasAudio { get; }

    public MediaFile? Media { get; }

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    public string FileName => Path.GetFileName(SourcePath);

    public string DurationText => DurationFormatter.Format(Duration);

    public string? ThumbnailPath => Kind == RenderSegmentKind.StillImage
        ? SourcePath
        : Media?.ThumbnailPath;

    public string KindText => Kind == RenderSegmentKind.StillImage ? "STILL SCREEN" : "VIDEO CLIP";

    public double CardWidth => Math.Clamp(90 + Duration.TotalSeconds * 1.5, 120, 280);

    public static TimelineClipViewModel FromMedia(MediaFile media, int order) => new(
        RenderSegmentKind.Video,
        media.FullPath,
        media.Duration,
        media.HasAudio,
        media,
        order);

    public static TimelineClipViewModel FromStillImage(
        string imagePath,
        TimeSpan duration,
        int order) => new(
            RenderSegmentKind.StillImage,
            imagePath,
            duration,
            hasAudio: false,
            media: null,
            order);

    public RenderSegment ToRenderSegment() => new(
        Kind,
        SourcePath,
        Duration,
        HasAudio,
        Media?.Id);
}
