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
        settings.MetadataFolder = ini.Get("Library", "MetadataFolder") ?? settings.MetadataFolder;
        settings.PreviewSlideCount = ReadInt(
            ini,
            "Library",
            "PreviewSlideCount",
            settings.PreviewSlideCount);
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
        return Normalize(settings);
    }

    public static string ToIni(ApplicationSettings source)
    {
        var settings = Normalize(source.Copy());
        var builder = new StringBuilder();
        builder.AppendLine("; Cat Clip Composer configuration");
        builder.AppendLine("; Stored beside the executable as requested. Paths are not quoted.");
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
        settings.FfmpegPath = string.IsNullOrWhiteSpace(settings.FfmpegPath)
            ? "ffmpeg.exe"
            : settings.FfmpegPath.Trim();
        settings.CustomFontFolder = string.IsNullOrWhiteSpace(settings.CustomFontFolder)
            ? Path.Combine(AppContext.BaseDirectory, "fonts")
            : settings.CustomFontFolder.Trim();
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

    private static TEnum ReadEnum<TEnum>(IniFile ini, string section, string key, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(ini.Get(section, key), ignoreCase: true, out var value)
            ? value
            : fallback;

    private static int ParseFolderIndex(string key) =>
        int.TryParse(key["Folder".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index
            : -1;

}
