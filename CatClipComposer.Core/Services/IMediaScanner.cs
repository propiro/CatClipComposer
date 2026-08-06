using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IMediaScanner
{
    Task<ScanResult> ScanAsync(
        ApplicationSettings settings,
        ScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
