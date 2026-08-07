namespace CatClipComposer.Core.Models;

public enum OutputOrientation
{
    Landscape,
    Portrait
}

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public enum VideoEncoderPreset
{
    NativeMpeg4,
    WindowsMediaFoundationH264,
    Libx264Gpl
}

public enum WorkspaceDockSlot
{
    Left,
    Center,
    Right,
    Bottom
}

public enum ContentBrowserViewMode
{
    List,
    SmallGrid,
    LargeGrid
}

public sealed class ApplicationSettings
{
    public List<string> SourceFolders { get; set; } = [];

    public string OutputFolder { get; set; } = string.Empty;

    public string ProjectFolder { get; set; } = string.Empty;

    public string MetadataFolder { get; set; } = string.Empty;

    public int PreviewSlideCount { get; set; } = 12;

    public ContentBrowserViewMode BrowserViewMode { get; set; } = ContentBrowserViewMode.SmallGrid;

    public int SmallThumbnailSize { get; set; } = 120;

    public int LargeThumbnailSize { get; set; } = 220;

    public string FfmpegPath { get; set; } = "ffmpeg.exe";

    public string CustomFontFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "fonts");

    public bool IncludeSubfolders { get; set; } = true;

    public bool ShowFileNames { get; set; } = true;

    public bool RescanLibraryOnStartup { get; set; } = true;

    public WorkspaceDockSlot ContentBrowserDock { get; set; } = WorkspaceDockSlot.Left;

    public WorkspaceDockSlot PreviewDock { get; set; } = WorkspaceDockSlot.Center;

    public WorkspaceDockSlot LayersDock { get; set; } = WorkspaceDockSlot.Right;

    public WorkspaceDockSlot TimelineDock { get; set; } = WorkspaceDockSlot.Bottom;

    public ApplicationSettings Copy() => new()
    {
        SourceFolders = [.. SourceFolders],
        OutputFolder = OutputFolder,
        ProjectFolder = ProjectFolder,
        MetadataFolder = MetadataFolder,
        PreviewSlideCount = PreviewSlideCount,
        BrowserViewMode = BrowserViewMode,
        SmallThumbnailSize = SmallThumbnailSize,
        LargeThumbnailSize = LargeThumbnailSize,
        FfmpegPath = FfmpegPath,
        CustomFontFolder = CustomFontFolder,
        IncludeSubfolders = IncludeSubfolders,
        ShowFileNames = ShowFileNames,
        RescanLibraryOnStartup = RescanLibraryOnStartup,
        ContentBrowserDock = ContentBrowserDock,
        PreviewDock = PreviewDock,
        LayersDock = LayersDock,
        TimelineDock = TimelineDock
    };
}
