namespace CatClipComposer.Core.Services;

public interface IPreviewSheetGenerator
{
    Task<string?> CreateAsync(
        string filePath,
        TimeSpan duration,
        DateTime lastWriteUtc,
        int slideCount,
        string ffmpegPath,
        bool forceRecreate = false,
        CancellationToken cancellationToken = default);
}
