using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface ISettingsStore
{
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
