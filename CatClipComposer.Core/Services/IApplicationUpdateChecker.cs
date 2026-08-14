using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IApplicationUpdateChecker
{
    Task<ApplicationUpdateInfo> CheckAsync(
        string currentVersion,
        string? currentRevision,
        CancellationToken cancellationToken = default);
}
