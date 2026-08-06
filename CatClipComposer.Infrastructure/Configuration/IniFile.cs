namespace CatClipComposer.Infrastructure.Configuration;

internal sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string section, string key) =>
        _sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value)
            ? value
            : null;

    public IReadOnlyDictionary<string, string> GetSection(string section) =>
        _sections.TryGetValue(section, out var values)
            ? values
            : EmptySection;

    public static async Task<IniFile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var result = new IniFile();
        if (!File.Exists(path))
        {
            return result;
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var currentSection = string.Empty;
        foreach (var sourceLine in lines)
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!result._sections.TryGetValue(currentSection, out var section))
            {
                section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result._sections[currentSection] = section;
            }

            section[key] = value;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> EmptySection { get; } =
        new Dictionary<string, string>();
}
