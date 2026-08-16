using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Core.Models;

public enum RenderSegmentKind
{
    Video,
    StillImage
}

public sealed record RenderSegment(
    RenderSegmentKind Kind,
    string SourcePath,
    TimeSpan Duration,
    bool HasAudio,
    long? MediaFileId = null,
    VideoFitMode FitMode = VideoFitMode.Fit,
    double FadeInSeconds = 0,
    double FadeOutSeconds = 0,
    double Volume = 1,
    TimeSpan TimelineStart = default,
    RenderPluginEffect? BackgroundEffect = null);

public sealed record RenderPluginEffect(
    ICatClipVideoEffectPlugin Plugin,
    TimeSpan Start,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Parameters,
    int TrackOrder = 0,
    Guid? ProjectItemId = null);

public enum RenderOverlayKind
{
    Text,
    Image,
    Video,
    ProgressBar
}

public sealed record RenderOverlay(
    RenderOverlayKind Kind,
    TimeSpan Start,
    TimeSpan Duration,
    string? Text = null,
    string? SourcePath = null,
    string? FontPath = null,
    string? FontFamily = null,
    int FontSize = 42,
    bool TextStrokeEnabled = true,
    string TextStrokeColor = "#000000",
    double TextStrokeWidth = 3,
    double TextStrokeSmoothness = 0,
    OverlayPosition Position = OverlayPosition.Center,
    ProgressBarStyle ProgressBarStyle = ProgressBarStyle.Solid,
    ProgressBarPosition ProgressBarPosition = ProgressBarPosition.Bottom,
    string ProgressColor = "#C8C0B2",
    int ProgressHeight = 10,
    VideoFitMode FitMode = VideoFitMode.Fit,
    int TrackOrder = 0,
    bool HasCustomTransform = false,
    double TransformX = 0.5,
    double TransformY = 0.5,
    double TransformScale = 1,
    double TransformRotationDegrees = 0,
    double Opacity = 1,
    double FadeInSeconds = 0,
    double FadeOutSeconds = 0,
    Guid? ProjectItemId = null);

public sealed record RenderAudioLayer(
    string SourcePath,
    TimeSpan Start,
    TimeSpan Duration,
    double Volume = 0.35,
    double FadeInSeconds = 0,
    double FadeOutSeconds = 0);

public sealed record RenderRequest(
    IReadOnlyList<RenderSegment> Segments,
    string OutputPath,
    OutputOrientation Orientation,
    VideoEncoderPreset VideoEncoder = VideoEncoderPreset.NativeMpeg4,
    double FramesPerSecond = 30,
    string? ProjectName = null,
    string? ProjectFilePath = null,
    int OutputWidth = 0,
    int OutputHeight = 0,
    int QualityPercent = 80,
    int VideoBitrateKbps = 8000,
    int AudioBitrateKbps = 192,
    string BackgroundColor = "#101010",
    IReadOnlyList<RenderOverlay>? TimedOverlays = null,
    IReadOnlyList<RenderAudioLayer>? AudioLayers = null,
    IReadOnlyList<RenderPluginEffect>? PluginEffects = null,
    TimeSpan? OutputRangeStart = null,
    TimeSpan? OutputRangeDuration = null,
    double PreviewScale = 1,
    bool PreserveSelectedObjectQuality = false,
    Guid? SelectedObjectId = null);

public sealed record RenderProgress(
    double Percent,
    TimeSpan ProcessedDuration,
    TimeSpan TotalDuration,
    string Message);

public sealed record RenderResult(
    string OutputPath,
    TimeSpan Duration);

public sealed record FfmpegCommandPreview(
    string CommandText,
    string OutputPath,
    string? SupportingFilesFolder);

public sealed record FfmpegCommandExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
