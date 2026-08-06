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
        settings.OutputFolder = ini.Get("Output", "Folder") ?? settings.OutputFolder;
        settings.TargetDurationMinutes = ReadDouble(
            ini,
            "Output",
            "TargetDurationMinutes",
            settings.TargetDurationMinutes);
        settings.Orientation = ReadEnum(
            ini,
            "Output",
            "Orientation",
            settings.Orientation);
        settings.FfmpegPath = ini.Get("Tools", "FfmpegPath") ?? settings.FfmpegPath;
        settings.ProgressStyle = ReadEnum(
            ini,
            "Overlays",
            "ProgressStyle",
            settings.ProgressStyle);
        settings.OverlayImagePath = ini.Get("Overlays", "ImagePath") ?? string.Empty;
        settings.OverlayText = UnescapeText(ini.Get("Overlays", "Text") ?? string.Empty);
        settings.OverlayFontPath = ini.Get("Overlays", "FontPath") ?? string.Empty;
        settings.OverlayTextSize = ReadInt(
            ini,
            "Overlays",
            "TextSize",
            settings.OverlayTextSize);
        settings.OverlayPosition = ReadEnum(
            ini,
            "Overlays",
            "Position",
            settings.OverlayPosition);
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
        for (var index = 0; index < settings.SourceFolders.Count; index++)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Folder{index}={settings.SourceFolders[index]}");
        }

        builder.AppendLine();
        builder.AppendLine("[Output]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Folder={settings.OutputFolder}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"TargetDurationMinutes={settings.TargetDurationMinutes:0.##}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Orientation={settings.Orientation}");
        builder.AppendLine();
        builder.AppendLine("[Tools]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"FfmpegPath={settings.FfmpegPath}");
        builder.AppendLine();
        builder.AppendLine("[Overlays]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ProgressStyle={settings.ProgressStyle}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ImagePath={settings.OverlayImagePath}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Text={EscapeText(settings.OverlayText)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"FontPath={settings.OverlayFontPath}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"TextSize={settings.OverlayTextSize}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Position={settings.OverlayPosition}");
        return builder.ToString();
    }

    private static ApplicationSettings CreateDefaults() => new()
    {
        OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
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
        settings.FfmpegPath = string.IsNullOrWhiteSpace(settings.FfmpegPath)
            ? "ffmpeg.exe"
            : settings.FfmpegPath.Trim();
        settings.TargetDurationMinutes = Math.Clamp(settings.TargetDurationMinutes, 1, 720);
        settings.OverlayImagePath = settings.OverlayImagePath?.Trim() ?? string.Empty;
        settings.OverlayText = settings.OverlayText?.Trim() ?? string.Empty;
        settings.OverlayFontPath = settings.OverlayFontPath?.Trim() ?? string.Empty;
        settings.OverlayTextSize = Math.Clamp(settings.OverlayTextSize, 8, 200);
        return settings;
    }

    private static bool ReadBool(IniFile ini, string section, string key, bool fallback) =>
        bool.TryParse(ini.Get(section, key), out var value) ? value : fallback;

    private static int ReadInt(IniFile ini, string section, string key, int fallback) =>
        int.TryParse(ini.Get(section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static double ReadDouble(IniFile ini, string section, string key, double fallback) =>
        double.TryParse(ini.Get(section, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
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

    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string UnescapeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index + 1 >= value.Length)
            {
                builder.Append(value[index]);
                continue;
            }

            var escaped = value[++index];
            builder.Append(escaped switch
            {
                'r' => '\r',
                'n' => '\n',
                't' => '\t',
                '\\' => '\\',
                _ => escaped
            });
        }

        return builder.ToString();
    }
}
