using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IMediaProbe
{
    Task<VideoMetadata> ProbeAsync(
        string filePath,
        string ffmpegPath,
        CancellationToken cancellationToken = default);
}
