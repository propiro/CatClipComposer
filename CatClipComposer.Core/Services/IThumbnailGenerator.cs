namespace CatClipComposer.Core.Services;

public interface IThumbnailGenerator
{
    Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        string ffmpegPath,
        CancellationToken cancellationToken = default);
}
