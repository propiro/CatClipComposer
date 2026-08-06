using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IProjectStore
{
    string RecoveryPath { get; }

    Task SaveAsync(
        EditorProject project,
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<EditorProject> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task SaveRecoveryAsync(
        EditorProject project,
        CancellationToken cancellationToken = default);

    Task<EditorProject?> LoadRecoveryAsync(CancellationToken cancellationToken = default);

    Task ClearRecoveryAsync(CancellationToken cancellationToken = default);
}
