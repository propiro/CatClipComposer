namespace CatClipComposer.Core.Models;

public sealed record MediaUsageEntry(
    long RenderJobId,
    string? ProjectName,
    string? ProjectFilePath,
    string OutputPath,
    DateTime ExportedUtc,
    int Occurrences);
