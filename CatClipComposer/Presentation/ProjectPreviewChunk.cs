using System.IO;

namespace CatClipComposer.Presentation;

internal sealed record ProjectPreviewChunk(
    string OutputPath,
    TimeSpan Start,
    TimeSpan Duration,
    DateTime RenderedUtc,
    int QualityPercent)
{
    public TimeSpan End => Start + Duration;

    public bool Contains(TimeSpan position) =>
        position >= Start && position < End;
}

internal sealed class ProjectPreviewChunkCatalog
{
    private readonly List<ProjectPreviewChunk> _chunks = [];

    public int Count => _chunks.Count;

    public ProjectPreviewChunk? MostRecent =>
        _chunks.OrderByDescending(chunk => chunk.RenderedUtc).FirstOrDefault();

    public void Clear() => _chunks.Clear();

    public void Replace(IEnumerable<ProjectPreviewChunk> chunks)
    {
        _chunks.Clear();
        foreach (var chunk in chunks.OrderBy(chunk => chunk.RenderedUtc))
        {
            Add(chunk);
        }
    }

    public void Add(ProjectPreviewChunk chunk)
    {
        var cacheGroup = GetCacheGroup(chunk.OutputPath);
        if (cacheGroup is not null)
        {
            _chunks.RemoveAll(existing =>
                GetCacheGroup(existing.OutputPath) is { } existingGroup &&
                !existingGroup.Equals(cacheGroup, StringComparison.OrdinalIgnoreCase));
        }

        _chunks.RemoveAll(existing =>
            existing.Start == chunk.Start && existing.Duration == chunk.Duration);
        _chunks.Add(chunk);
    }

    public ProjectPreviewChunk? Find(TimeSpan position) =>
        _chunks
            .Where(chunk => chunk.Contains(position))
            .OrderByDescending(chunk => chunk.RenderedUtc)
            .FirstOrDefault();

    private static string? GetCacheGroup(string outputPath)
    {
        var parts = Path.GetFileNameWithoutExtension(outputPath).Split('-');
        return parts.Length >= 6 && parts[0].Length == 32 && parts[1].Length == 16
            ? $"{parts[0]}-{parts[1]}"
            : null;
    }
}
