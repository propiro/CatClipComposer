namespace CatClipComposer.Presentation;

internal sealed record ProjectPreviewCoverageRange(
    TimeSpan Start,
    TimeSpan End,
    bool NeedsRender);

internal sealed class ProjectPreviewCoverageCatalog
{
    private readonly List<ProjectPreviewCoverageRange> _ranges = [];

    public IReadOnlyList<ProjectPreviewCoverageRange> Ranges => _ranges;

    public void Clear() => _ranges.Clear();

    public bool IsCurrent(TimeSpan start, TimeSpan end) => _ranges.Any(range =>
        !range.NeedsRender && start >= range.Start && end <= range.End);

    public void MarkRendered(TimeSpan start, TimeSpan end) => ReplaceRange(start, end, needsRender: false);

    public void MarkStale(TimeSpan start, TimeSpan end)
    {
        if (end <= start)
        {
            return;
        }

        var updated = new List<ProjectPreviewCoverageRange>();
        foreach (var coverage in _ranges)
        {
            if (coverage.End <= start || coverage.Start >= end)
            {
                updated.Add(coverage);
                continue;
            }

            if (coverage.Start < start)
            {
                updated.Add(coverage with { End = start });
            }

            updated.Add(new ProjectPreviewCoverageRange(
                coverage.Start > start ? coverage.Start : start,
                coverage.End < end ? coverage.End : end,
                NeedsRender: true));

            if (coverage.End > end)
            {
                updated.Add(coverage with { Start = end });
            }
        }

        ReplaceWithNormalized(updated);
    }

    private void ReplaceRange(TimeSpan start, TimeSpan end, bool needsRender)
    {
        if (end <= start)
        {
            return;
        }

        var updated = new List<ProjectPreviewCoverageRange>();
        foreach (var coverage in _ranges)
        {
            if (coverage.End <= start || coverage.Start >= end)
            {
                updated.Add(coverage);
                continue;
            }

            if (coverage.Start < start)
            {
                updated.Add(coverage with { End = start });
            }

            if (coverage.End > end)
            {
                updated.Add(coverage with { Start = end });
            }
        }

        updated.Add(new ProjectPreviewCoverageRange(start, end, needsRender));
        ReplaceWithNormalized(updated);
    }

    private void ReplaceWithNormalized(IEnumerable<ProjectPreviewCoverageRange> ranges)
    {
        _ranges.Clear();
        foreach (var range in ranges
                     .Where(range => range.End > range.Start)
                     .OrderBy(range => range.Start)
                     .ThenBy(range => range.End))
        {
            var previous = _ranges.LastOrDefault();
            if (previous is not null && previous.NeedsRender == range.NeedsRender && previous.End >= range.Start)
            {
                _ranges[^1] = previous with
                {
                    End = previous.End > range.End ? previous.End : range.End
                };
            }
            else
            {
                _ranges.Add(range);
            }
        }
    }
}

public sealed record ProjectPreviewCoverageSegmentViewModel(
    double Left,
    double Width,
    bool NeedsRender)
{
    public string Color => NeedsRender ? "#E0B44E" : "#56C77A";

    public string ToolTip => NeedsRender
        ? "This cached preview interval is stale because its project content changed. Prerender it again."
        : "This project interval has a current prerendered preview.";
}
