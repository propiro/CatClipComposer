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
    long? MediaFileId = null);

public sealed record RenderRequest(
    IReadOnlyList<RenderSegment> Segments,
    string OutputPath,
    OutputOrientation Orientation,
    VideoProgressStyle ProgressStyle = VideoProgressStyle.None,
    string? OverlayImagePath = null,
    string? OverlayText = null,
    string? OverlayFontPath = null,
    int OverlayTextSize = 42,
    OverlayPosition OverlayPosition = OverlayPosition.TopRight,
    VideoEncoderPreset VideoEncoder = VideoEncoderPreset.NativeMpeg4,
    int FramesPerSecond = 30,
    string? ProjectName = null,
    string? ProjectFilePath = null);

public sealed record RenderProgress(
    double Percent,
    TimeSpan ProcessedDuration,
    TimeSpan TotalDuration,
    string Message);

public sealed record RenderResult(
    string OutputPath,
    TimeSpan Duration);
