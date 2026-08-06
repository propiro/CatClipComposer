using CatClipComposer.Core.Models;
using CatClipComposer.Core.Utilities;
using CatClipComposer.Core.Plugins;
using System.IO;

namespace CatClipComposer.Presentation;

public sealed class TimelineClipViewModel : ObservableObject
{
    private int _order;
    private VideoFitMode _fitMode;
    private double _fadeInSeconds;
    private double _fadeOutSeconds;
    private double _volume;

    private TimelineClipViewModel(
        RenderSegmentKind kind,
        string sourcePath,
        TimeSpan duration,
        bool hasAudio,
        MediaFile? media,
        int order,
        Guid? instanceId = null,
        VideoFitMode fitMode = VideoFitMode.Fit,
        double fadeInSeconds = 0,
        double fadeOutSeconds = 0,
        double volume = 1,
        string pluginId = "")
    {
        Kind = kind;
        SourcePath = sourcePath;
        Duration = duration;
        HasAudio = hasAudio;
        Media = media;
        _order = order;
        InstanceId = instanceId ?? Guid.NewGuid();
        _fitMode = fitMode;
        _fadeInSeconds = fadeInSeconds;
        _fadeOutSeconds = fadeOutSeconds;
        _volume = volume;
        PluginId = pluginId;
    }

    public Guid InstanceId { get; }

    public RenderSegmentKind Kind { get; }

    public string SourcePath { get; }

    public TimeSpan Duration { get; }

    public bool HasAudio { get; }

    public MediaFile? Media { get; }

    public VideoFitMode FitMode => _fitMode;

    public double FadeInSeconds => _fadeInSeconds;

    public double FadeOutSeconds => _fadeOutSeconds;

    public double Volume => _volume;

    public string PluginId { get; }

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
        order,
        pluginId: BuiltInPluginIds.PngSplashScreen);

    public static TimelineClipViewModel FromProjectItem(
        ProjectTimelineItem item,
        MediaFile? media,
        int order) => new(
        item.Kind == ProjectItemKind.StillImage
            ? RenderSegmentKind.StillImage
            : RenderSegmentKind.Video,
        item.SourcePath,
        item.Duration,
        item.HasAudio,
        media,
        order,
        item.Id,
        item.FitMode,
        item.FadeInSeconds,
        item.FadeOutSeconds,
        item.Volume,
        item.PluginId);

    public ProjectTimelineItem ToProjectItem(TimeSpan start) => new()
    {
        Id = InstanceId,
        Kind = Kind == RenderSegmentKind.StillImage
            ? ProjectItemKind.StillImage
            : ProjectItemKind.Video,
        Name = FileName,
        SourcePath = SourcePath,
        MediaFileId = Media?.Id,
        StartTicks = start.Ticks,
        DurationTicks = Duration.Ticks,
        HasAudio = HasAudio,
        FitMode = FitMode,
        FadeInSeconds = FadeInSeconds,
        FadeOutSeconds = FadeOutSeconds,
        Volume = Volume,
        PluginId = PluginId
    };

    public RenderSegment ToRenderSegment() => new(
        Kind,
        SourcePath,
        Duration,
        HasAudio,
        Media?.Id,
        FitMode,
        FadeInSeconds,
        FadeOutSeconds,
        Volume);

    public void UpdateEffects(
        VideoFitMode fitMode,
        double fadeInSeconds,
        double fadeOutSeconds,
        double volume)
    {
        if (_fitMode != fitMode)
        {
            _fitMode = fitMode;
            OnPropertyChanged(nameof(FitMode));
        }

        if (!double.Equals(_fadeInSeconds, fadeInSeconds))
        {
            _fadeInSeconds = fadeInSeconds;
            OnPropertyChanged(nameof(FadeInSeconds));
        }

        if (!double.Equals(_fadeOutSeconds, fadeOutSeconds))
        {
            _fadeOutSeconds = fadeOutSeconds;
            OnPropertyChanged(nameof(FadeOutSeconds));
        }

        if (!double.Equals(_volume, volume))
        {
            _volume = volume;
            OnPropertyChanged(nameof(Volume));
        }
    }
}
