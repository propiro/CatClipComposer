using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IFfmpegCommandService
{
    Task<FfmpegCommandPreview> CreateAsync(
        RenderRequest request,
        string ffmpegPath,
        string supportingFilesRoot,
        CancellationToken cancellationToken = default);

    Task<FfmpegCommandExecutionResult> ExecuteAsync(
        string commandText,
        string requiredFfmpegPath,
        CancellationToken cancellationToken = default);
}
