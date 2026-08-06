using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public sealed class CompositionExportService(
    IVideoRenderer videoRenderer,
    IMediaCatalog mediaCatalog) : ICompositionExporter
{
    public async Task<RenderResult> ExportAsync(
        RenderRequest request,
        string ffmpegPath,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await videoRenderer.RenderAsync(
            request,
            ffmpegPath,
            progress,
            cancellationToken);

        var mediaFileIds = request.Segments
            .Where(segment => segment.MediaFileId.HasValue)
            .Select(segment => segment.MediaFileId!.Value)
            .ToList();
        await mediaCatalog.RecordExportAsync(
            result.OutputPath,
            result.Duration,
            mediaFileIds,
            request.ProjectName,
            request.ProjectFilePath,
            cancellationToken);

        return result;
    }
}
