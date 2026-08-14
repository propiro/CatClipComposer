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

public enum WorkspacePanelSelection
{
    ContentBrowser,
    Preview,
    Layers,
    Timeline
}

public enum ContentBrowserViewMode
{
    List,
    SmallGrid,
    LargeGrid,
    ExtraLargeGrid
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

    public int ExtraLargeThumbnailSize { get; set; } = 420;

    public int PreviewQualityPercent { get; set; } = 50;

    public bool PreserveSelectedPreviewObjectQuality { get; set; } = true;

    public string FfmpegPath { get; set; } = "ffmpeg.exe";

    public string CustomFontFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "fonts");

    public bool IncludeSubfolders { get; set; } = true;

    public bool ShowFileNames { get; set; } = true;

    public bool RescanLibraryOnStartup { get; set; } = true;

    public bool FirstStartupCompleted { get; set; }

    public List<string> RecentProjectPaths { get; set; } = [];

    public ProgressBarStyle DefaultProgressBarStyle { get; set; } = ProgressBarStyle.Solid;

    public ProgressBarPosition DefaultProgressBarPosition { get; set; } = ProgressBarPosition.Bottom;

    public string DefaultProgressColor { get; set; } = "#C8C0B2";

    public int DefaultProgressHeight { get; set; } = 10;

    public WorkspaceDockSlot ContentBrowserDock { get; set; } = WorkspaceDockSlot.Left;

    public WorkspaceDockSlot PreviewDock { get; set; } = WorkspaceDockSlot.Center;

    public WorkspaceDockSlot LayersDock { get; set; } = WorkspaceDockSlot.Right;

    public WorkspaceDockSlot TimelineDock { get; set; } = WorkspaceDockSlot.Bottom;

    public double WindowWidth { get; set; } = 1440;

    public double WindowHeight { get; set; } = 900;

    public double WindowLeft { get; set; } = -1;

    public double WindowTop { get; set; } = -1;

    public bool WindowMaximized { get; set; }

    public double WorkspaceLeftWidth { get; set; } = 310;

    public double WorkspaceRightWidth { get; set; } = 270;

    public double WorkspaceBottomHeight { get; set; } = 270;

    public double TimelinePixelsPerSecond { get; set; } = 8;

    public double TimelineTrackHeight { get; set; } = 64;

    public bool PreviewsSplit { get; set; }

    public double PreviewSplitRatio { get; set; } = 0.5;

    public int ActivePreviewTab { get; set; }

    public WorkspacePanelSelection ActiveWorkspacePanel { get; set; } = WorkspacePanelSelection.ContentBrowser;

    public WorkspacePanelSelection? ExpandedWorkspacePanel { get; set; }

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
        ExtraLargeThumbnailSize = ExtraLargeThumbnailSize,
        PreviewQualityPercent = PreviewQualityPercent,
        PreserveSelectedPreviewObjectQuality = PreserveSelectedPreviewObjectQuality,
        FfmpegPath = FfmpegPath,
        CustomFontFolder = CustomFontFolder,
        IncludeSubfolders = IncludeSubfolders,
        ShowFileNames = ShowFileNames,
        RescanLibraryOnStartup = RescanLibraryOnStartup,
        FirstStartupCompleted = FirstStartupCompleted,
        RecentProjectPaths = [.. RecentProjectPaths],
        DefaultProgressBarStyle = DefaultProgressBarStyle,
        DefaultProgressBarPosition = DefaultProgressBarPosition,
        DefaultProgressColor = DefaultProgressColor,
        DefaultProgressHeight = DefaultProgressHeight,
        ContentBrowserDock = ContentBrowserDock,
        PreviewDock = PreviewDock,
        LayersDock = LayersDock,
        TimelineDock = TimelineDock,
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
        WindowMaximized = WindowMaximized,
        WorkspaceLeftWidth = WorkspaceLeftWidth,
        WorkspaceRightWidth = WorkspaceRightWidth,
        WorkspaceBottomHeight = WorkspaceBottomHeight,
        TimelinePixelsPerSecond = TimelinePixelsPerSecond,
        TimelineTrackHeight = TimelineTrackHeight,
        PreviewsSplit = PreviewsSplit,
        PreviewSplitRatio = PreviewSplitRatio,
        ActivePreviewTab = ActivePreviewTab,
        ActiveWorkspacePanel = ActiveWorkspacePanel,
        ExpandedWorkspacePanel = ExpandedWorkspacePanel
    };
}
