namespace CatClipComposer.Core.Models;

public sealed class MediaFile
{
    public long Id { get; init; }

    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public required string Extension { get; init; }

    public long DurationTicks { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public bool HasAudio { get; init; }

    public long FileSize { get; init; }

    public DateTime LastWriteUtc { get; init; }

    public string? ThumbnailPath { get; init; }

    public string? PreviewSheetPath { get; init; }

    public string Tags { get; init; } = string.Empty;

    public DateTime DiscoveredUtc { get; init; }

    public DateTime LastScannedUtc { get; init; }

    public bool IsAvailable { get; init; } = true;

    public int UseCount { get; init; }

    public DateTime? LastUsedUtc { get; init; }

    public string? LastOutputPath { get; init; }

    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);
}
