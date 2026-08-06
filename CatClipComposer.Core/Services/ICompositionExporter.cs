using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface ICompositionExporter
{
    Task<RenderResult> ExportAsync(
        RenderRequest request,
        string ffmpegPath,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
