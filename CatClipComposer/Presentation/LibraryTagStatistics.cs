namespace CatClipComposer.Presentation;

public static class LibraryTagStatistics
{
    public static IReadOnlyList<string> GetMostUsed(
        IEnumerable<string> libraryTagValues,
        int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(libraryTagValues);
        if (limit <= 0)
        {
            return [];
        }

        var counts = new Dictionary<string, (string DisplayName, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in libraryTagValues)
        {
            foreach (var tag in Parse(value).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                counts[tag] = counts.TryGetValue(tag, out var current)
                    ? (current.DisplayName, current.Count + 1)
                    : (tag, 1);
            }
        }

        return counts.Values
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(entry => entry.DisplayName)
            .ToList();
    }

    public static IEnumerable<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
