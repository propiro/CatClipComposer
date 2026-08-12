using System.Globalization;
using System.Text;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Infrastructure.Configuration;

internal static class ApplicationSettingsIniMapper
{
    public static ApplicationSettings FromIni(IniFile ini)
    {
        var settings = CreateDefaults();
        settings.SourceFolders = ini.GetSection("Sources")
            .Where(pair => pair.Key.StartsWith("Folder", StringComparison.OrdinalIgnoreCase))
            .Select(pair => new
            {
                Value = pair.Value,
                Index = ParseFolderIndex(pair.Key)
            })
            .Where(item => item.Index >= 0 && !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Index)
            .Select(item => item.Value)
            .ToList();
        settings.IncludeSubfolders = ReadBool(
            ini,
            "Sources",
            "IncludeSubfolders",
            settings.IncludeSubfolders);
        settings.ShowFileNames = ReadBool(
            ini,
            "Sources",
            "ShowFileNames",
            settings.ShowFileNames);
        settings.RescanLibraryOnStartup = ReadBool(
            ini,
            "Sources",
            "RescanLibraryOnStartup",
            settings.RescanLibraryOnStartup);
        settings.FirstStartupCompleted = ReadBool(
            ini,
            "Startup",
            "FirstStartupCompleted",
            settings.FirstStartupCompleted);
        settings.RecentProjectPaths = ini.GetSection("RecentProjects")
            .Where(pair => pair.Key.StartsWith("Project", StringComparison.OrdinalIgnoreCase))
            .Select(pair => new { pair.Value, Index = ParseIndexedKey(pair.Key, "Project") })
            .Where(item => item.Index >= 0 && !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Index)
            .Select(item => item.Value)
            .ToList();
        settings.DefaultProgressBarStyle = ReadEnum(
            ini, "ProgressDefaults", "Style", settings.DefaultProgressBarStyle);
        settings.DefaultProgressBarPosition = ReadEnum(
            ini, "ProgressDefaults", "Position", settings.DefaultProgressBarPosition);
        settings.DefaultProgressColor = ini.Get("ProgressDefaults", "Color") ?? settings.DefaultProgressColor;
        settings.DefaultProgressHeight = ReadInt(
            ini, "ProgressDefaults", "Height", settings.DefaultProgressHeight);
        settings.MetadataFolder = ini.Get("Library", "MetadataFolder") ?? settings.MetadataFolder;
        settings.PreviewSlideCount = ReadInt(
            ini,
            "Library",
            "PreviewSlideCount",
            settings.PreviewSlideCount);
        settings.BrowserViewMode = ReadEnum(
            ini,
            "Library",
            "BrowserViewMode",
            settings.BrowserViewMode);
        settings.SmallThumbnailSize = ReadInt(
            ini,
            "Library",
            "SmallThumbnailSize",
            settings.SmallThumbnailSize);
        settings.LargeThumbnailSize = ReadInt(
            ini,
            "Library",
            "LargeThumbnailSize",
            settings.LargeThumbnailSize);
        settings.OutputFolder = ini.Get("Output", "Folder") ?? settings.OutputFolder;
        settings.ProjectFolder = ini.Get("Output", "ProjectFolder") ?? settings.ProjectFolder;
        settings.FfmpegPath = ini.Get("Tools", "FfmpegPath") ?? settings.FfmpegPath;
        settings.CustomFontFolder = ini.Get("Tools", "CustomFontFolder") ?? settings.CustomFontFolder;
        settings.ContentBrowserDock = ReadEnum(
            ini,
            "Workspace",
            "ContentBrowserDock",
            settings.ContentBrowserDock);
        settings.PreviewDock = ReadEnum(
            ini,
            "Workspace",
            "PreviewDock",
            settings.PreviewDock);
        settings.LayersDock = ReadEnum(
            ini,
            "Workspace",
            "LayersDock",
            settings.LayersDock);
        settings.TimelineDock = ReadEnum(
            ini,
            "Workspace",
            "TimelineDock",
            settings.TimelineDock);
        settings.WindowWidth = ReadDouble(ini, "Workspace", "WindowWidth", settings.WindowWidth);
        settings.WindowHeight = ReadDouble(ini, "Workspace", "WindowHeight", settings.WindowHeight);
        settings.WindowLeft = ReadDouble(ini, "Workspace", "WindowLeft", settings.WindowLeft);
        settings.WindowTop = ReadDouble(ini, "Workspace", "WindowTop", settings.WindowTop);
        settings.WindowMaximized = ReadBool(ini, "Workspace", "WindowMaximized", settings.WindowMaximized);
        settings.WorkspaceLeftWidth = ReadDouble(
            ini, "Workspace", "WorkspaceLeftWidth", settings.WorkspaceLeftWidth);
        settings.WorkspaceRightWidth = ReadDouble(
            ini, "Workspace", "WorkspaceRightWidth", settings.WorkspaceRightWidth);
        settings.WorkspaceBottomHeight = ReadDouble(
            ini, "Workspace", "WorkspaceBottomHeight", settings.WorkspaceBottomHeight);
        settings.PreviewsSplit = ReadBool(ini, "Workspace", "PreviewsSplit", settings.PreviewsSplit);
        settings.PreviewSplitRatio = ReadDouble(
            ini, "Workspace", "PreviewSplitRatio", settings.PreviewSplitRatio);
        settings.ActivePreviewTab = ReadInt(
            ini, "Workspace", "ActivePreviewTab", settings.ActivePreviewTab);
        settings.ActiveWorkspacePanel = ReadEnum(
            ini, "Workspace", "ActiveWorkspacePanel", settings.ActiveWorkspacePanel);
        settings.ExpandedWorkspacePanel = ReadNullableEnum<WorkspacePanelSelection>(
            ini, "Workspace", "ExpandedWorkspacePanel");
        return Normalize(settings);
    }

    public static string ToIni(ApplicationSettings source)
    {
        var settings = Normalize(source.Copy());
        var builder = new StringBuilder();
        builder.AppendLine("; Cat Clip Composer configuration");
        builder.AppendLine("; Stored beside the executable as requested. Paths are not quoted.");
        builder.AppendLine();
        builder.AppendLine("[Startup]");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"FirstStartupCompleted={settings.FirstStartupCompleted.ToString().ToLowerInvariant()}");
        builder.AppendLine();
        builder.AppendLine("[RecentProjects]");
        for (var index = 0; index < settings.RecentProjectPaths.Count; index++)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Project{index}={settings.RecentProjectPaths[index]}");
        }
        builder.AppendLine();
        builder.AppendLine("[ProgressDefaults]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Style={settings.DefaultProgressBarStyle}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Position={settings.DefaultProgressBarPosition}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Color={settings.DefaultProgressColor}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Height={settings.DefaultProgressHeight}");
        builder.AppendLine();
        builder.AppendLine("[Sources]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"IncludeSubfolders={settings.IncludeSubfolders.ToString().ToLowerInvariant()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ShowFileNames={settings.ShowFileNames.ToString().ToLowerInvariant()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"RescanLibraryOnStartup={settings.RescanLibraryOnStartup.ToString().ToLowerInvariant()}");
        for (var index = 0; index < settings.SourceFolders.Count; index++)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Folder{index}={settings.SourceFolders[index]}");
        }

        builder.AppendLine();
        builder.AppendLine("[Library]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"MetadataFolder={settings.MetadataFolder}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"PreviewSlideCount={settings.PreviewSlideCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"BrowserViewMode={settings.BrowserViewMode}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"SmallThumbnailSize={settings.SmallThumbnailSize}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"LargeThumbnailSize={settings.LargeThumbnailSize}");

        builder.AppendLine();
        builder.AppendLine("[Output]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Folder={settings.OutputFolder}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ProjectFolder={settings.ProjectFolder}");
        builder.AppendLine();
        builder.AppendLine("[Tools]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"FfmpegPath={settings.FfmpegPath}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"CustomFontFolder={settings.CustomFontFolder}");
        builder.AppendLine();
        builder.AppendLine("[Workspace]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ContentBrowserDock={settings.ContentBrowserDock}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"PreviewDock={settings.PreviewDock}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"LayersDock={settings.LayersDock}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"TimelineDock={settings.TimelineDock}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowWidth={settings.WindowWidth:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowHeight={settings.WindowHeight:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowLeft={settings.WindowLeft:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowTop={settings.WindowTop:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowMaximized={settings.WindowMaximized.ToString().ToLowerInvariant()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WorkspaceLeftWidth={settings.WorkspaceLeftWidth:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WorkspaceRightWidth={settings.WorkspaceRightWidth:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WorkspaceBottomHeight={settings.WorkspaceBottomHeight:0.###}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"PreviewsSplit={settings.PreviewsSplit.ToString().ToLowerInvariant()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"PreviewSplitRatio={settings.PreviewSplitRatio:0.######}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ActivePreviewTab={settings.ActivePreviewTab}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ActiveWorkspacePanel={settings.ActiveWorkspacePanel}");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"ExpandedWorkspacePanel={settings.ExpandedWorkspacePanel?.ToString() ?? string.Empty}");
        return builder.ToString();
    }

    private static ApplicationSettings CreateDefaults() => new()
    {
        OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        ProjectFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "CatClipComposer Projects"),
        MetadataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatClipComposer"),
        CustomFontFolder = Path.Combine(AppContext.BaseDirectory, "fonts")
    };

    private static ApplicationSettings Normalize(ApplicationSettings settings)
    {
        settings.SourceFolders = settings.SourceFolders
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : settings.OutputFolder.Trim();
        settings.ProjectFolder = string.IsNullOrWhiteSpace(settings.ProjectFolder)
            ? Path.Combine(settings.OutputFolder, "Projects")
            : settings.ProjectFolder.Trim();
        settings.MetadataFolder = string.IsNullOrWhiteSpace(settings.MetadataFolder)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CatClipComposer")
            : settings.MetadataFolder.Trim();
        settings.PreviewSlideCount = Math.Clamp(settings.PreviewSlideCount, 1, 24);
        settings.SmallThumbnailSize = Math.Clamp(settings.SmallThumbnailSize, 80, 200);
        settings.LargeThumbnailSize = Math.Clamp(settings.LargeThumbnailSize, 140, 360);
        settings.LargeThumbnailSize = Math.Max(settings.LargeThumbnailSize, settings.SmallThumbnailSize + 20);
        settings.RecentProjectPaths = settings.RecentProjectPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        settings.DefaultProgressColor = IsHexColor(settings.DefaultProgressColor)
            ? settings.DefaultProgressColor.ToUpperInvariant()
            : "#C8C0B2";
        settings.DefaultProgressHeight = Math.Clamp(settings.DefaultProgressHeight, 2, 100);
        settings.FfmpegPath = string.IsNullOrWhiteSpace(settings.FfmpegPath)
            ? "ffmpeg.exe"
            : settings.FfmpegPath.Trim();
        settings.CustomFontFolder = string.IsNullOrWhiteSpace(settings.CustomFontFolder)
            ? Path.Combine(AppContext.BaseDirectory, "fonts")
            : settings.CustomFontFolder.Trim();
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 1040, 10000);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight, 680, 10000);
        settings.WindowLeft = double.IsFinite(settings.WindowLeft)
            ? Math.Clamp(settings.WindowLeft, -100000, 100000)
            : -1;
        settings.WindowTop = double.IsFinite(settings.WindowTop)
            ? Math.Clamp(settings.WindowTop, -100000, 100000)
            : -1;
        settings.WorkspaceLeftWidth = Math.Clamp(settings.WorkspaceLeftWidth, 190, 3000);
        settings.WorkspaceRightWidth = Math.Clamp(settings.WorkspaceRightWidth, 190, 3000);
        settings.WorkspaceBottomHeight = Math.Clamp(settings.WorkspaceBottomHeight, 150, 3000);
        settings.PreviewSplitRatio = Math.Clamp(settings.PreviewSplitRatio, 0.15, 0.85);
        settings.ActivePreviewTab = Math.Clamp(settings.ActivePreviewTab, 0, 1);
        NormalizeWorkspace(settings);
        return settings;
    }

    private static void NormalizeWorkspace(ApplicationSettings settings)
    {
        var slots = new[]
        {
            settings.ContentBrowserDock,
            settings.PreviewDock,
            settings.LayersDock,
            settings.TimelineDock
        };
        if (slots.Distinct().Count() == slots.Length)
        {
            return;
        }

        settings.ContentBrowserDock = WorkspaceDockSlot.Left;
        settings.PreviewDock = WorkspaceDockSlot.Center;
        settings.LayersDock = WorkspaceDockSlot.Right;
        settings.TimelineDock = WorkspaceDockSlot.Bottom;
    }

    private static bool ReadBool(IniFile ini, string section, string key, bool fallback) =>
        bool.TryParse(ini.Get(section, key), out var value) ? value : fallback;

    private static int ReadInt(IniFile ini, string section, string key, int fallback) =>
        int.TryParse(ini.Get(section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static int ParseIndexedKey(string key, string prefix) =>
        key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(key[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : -1;

    private static double ReadDouble(IniFile ini, string section, string key, double fallback) =>
        double.TryParse(ini.Get(section, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
        double.IsFinite(value)
            ? value
            : fallback;

    private static TEnum ReadEnum<TEnum>(IniFile ini, string section, string key, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(ini.Get(section, key), ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : fallback;

    private static TEnum? ReadNullableEnum<TEnum>(IniFile ini, string section, string key)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(ini.Get(section, key), ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : null;

    private static int ParseFolderIndex(string key) =>
        int.TryParse(key["Folder".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index
            : -1;

}
