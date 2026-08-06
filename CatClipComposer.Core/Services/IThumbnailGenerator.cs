namespace CatClipComposer.Core.Services;

public interface IThumbnailGenerator
{
    Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        string ffmpegPath,
        bool forceRecreate = false,
        CancellationToken cancellationToken = default);
}
