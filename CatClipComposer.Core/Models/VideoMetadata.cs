namespace CatClipComposer.Core.Models;

public sealed record VideoMetadata(
    TimeSpan Duration,
    int Width,
    int Height,
    bool HasAudio);
