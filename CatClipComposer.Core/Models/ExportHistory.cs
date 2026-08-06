namespace CatClipComposer.Core.Models;

public sealed record ExportHistoryClip(
    int Order,
    long MediaFileId,
    string FileName,
    string FullPath);

public sealed record ExportHistoryEntry(
    long Id,
    string OutputPath,
    TimeSpan Duration,
    DateTime CreatedUtc,
    IReadOnlyList<ExportHistoryClip> Clips);
