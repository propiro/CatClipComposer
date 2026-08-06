using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IMediaScanner
{
    Task<ScanResult> ScanAsync(
        ApplicationSettings settings,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
