namespace CatClipComposer.Presentation;

internal sealed class ProjectPreviewCacheEntry
{
    public string ProjectFingerprint { get; set; } = string.Empty;

    public string OutputPath { get; set; } = string.Empty;

    public long RangeStartTicks { get; set; }

    public long DurationTicks { get; set; }

    public DateTime RenderedUtc { get; set; }

    public int PreviewQualityPercent { get; set; } = 100;

    public bool PreserveSelectedObjectQuality { get; set; }

    public Guid? SelectedObjectId { get; set; }

    public TimeSpan RangeStart => TimeSpan.FromTicks(RangeStartTicks);

    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

    public TimeSpan RangeEnd => RangeStart + Duration;
}
