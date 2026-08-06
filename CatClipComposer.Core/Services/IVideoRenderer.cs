using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IVideoRenderer
{
    Task<RenderResult> RenderAsync(
        RenderRequest request,
        string ffmpegPath,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
