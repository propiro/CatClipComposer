using System.Globalization;
using System.IO;
using System.Windows.Media;

namespace CatClipComposer.Presentation;

public sealed record FontChoice(
    string FamilyName,
    string FilePath,
    bool IsCustom)
{
    public string SourceBadge => IsCustom ? "◆ CUSTOM FOLDER" : "SYSTEM";

    public string Details => IsCustom ? FilePath : "Installed in Windows";

    public FontFamily PreviewFontFamily => new(FamilyName);
}

public static class FontCatalog
{
    public static IReadOnlyList<FontChoice> Load(string customFolder)
    {
        var choices = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Select(name => new FontChoice(name, string.Empty, IsCustom: false))
            .ToList();

        if (!Directory.Exists(customFolder))
        {
            return choices;
        }

        foreach (var file in Directory.EnumerateFiles(customFolder, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => Path.GetExtension(path) is ".ttf" or ".otf" or ".TTF" or ".OTF")
                     .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            choices.Insert(0, new FontChoice(ReadFamilyName(file), Path.GetFullPath(file), IsCustom: true));
        }

        return choices;
    }

    private static string ReadFamilyName(string path)
    {
        try
        {
            var typeface = new GlyphTypeface(new Uri(Path.GetFullPath(path), UriKind.Absolute));
            if (typeface.Win32FamilyNames.TryGetValue(CultureInfo.CurrentUICulture, out var localized))
            {
                return localized;
            }

            return typeface.Win32FamilyNames.Values.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }
}
