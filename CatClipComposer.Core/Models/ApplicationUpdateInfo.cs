namespace CatClipComposer.Core.Models;

public sealed record ApplicationUpdateInfo(
    string CurrentVersion,
    string? CurrentRevision,
    string? LatestCodeVersion,
    string? LatestCodeRevision,
    string? LatestBinaryVersion,
    bool BinaryPackageFound,
    bool IsCodeUpdateAvailable,
    bool IsBinaryUpdateAvailable,
    Uri RepositoryUri,
    Uri ReleasesUri,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CheckedAtUtc);
