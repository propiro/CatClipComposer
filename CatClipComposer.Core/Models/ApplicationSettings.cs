namespace CatClipComposer.Core.Models;

public enum OutputOrientation
{
    Landscape,
    Portrait
}

public enum VideoProgressStyle
{
    None,
    WholeCompilation,
    EachClip
}

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public sealed class ApplicationSettings
{
    public List<string> SourceFolders { get; set; } = [];

    public string OutputFolder { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = "ffmpeg.exe";

    public double TargetDurationMinutes { get; set; } = 15;

    public OutputOrientation Orientation { get; set; } = OutputOrientation.Landscape;

    public bool IncludeSubfolders { get; set; } = true;

    public bool ShowFileNames { get; set; } = true;

    public VideoProgressStyle ProgressStyle { get; set; } = VideoProgressStyle.WholeCompilation;

    public string OverlayImagePath { get; set; } = string.Empty;

    public string OverlayText { get; set; } = string.Empty;

    public string OverlayFontPath { get; set; } = string.Empty;

    public int OverlayTextSize { get; set; } = 42;

    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopRight;

    public ApplicationSettings Copy() => new()
    {
        SourceFolders = [.. SourceFolders],
        OutputFolder = OutputFolder,
        FfmpegPath = FfmpegPath,
        TargetDurationMinutes = TargetDurationMinutes,
        Orientation = Orientation,
        IncludeSubfolders = IncludeSubfolders,
        ShowFileNames = ShowFileNames,
        ProgressStyle = ProgressStyle,
        OverlayImagePath = OverlayImagePath,
        OverlayText = OverlayText,
        OverlayFontPath = OverlayFontPath,
        OverlayTextSize = OverlayTextSize,
        OverlayPosition = OverlayPosition
    };
}
