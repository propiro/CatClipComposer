namespace CatClipComposer.Core.Models;

public sealed record ScanProgress(
    int Processed,
    int Total,
    int Added,
    int Updated,
    int Failed,
    string CurrentFile);

public sealed record ScanResult(
    int Discovered,
    int Added,
    int Updated,
    int Failed,
    IReadOnlyList<string> Errors);
