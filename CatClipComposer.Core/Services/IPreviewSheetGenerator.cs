namespace CatClipComposer.Core.Services;

public interface IPreviewSheetGenerator
{
    Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        int slideCount,
        string ffmpegPath,
        CancellationToken cancellationToken = default);
}
