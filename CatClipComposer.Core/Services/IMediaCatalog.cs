using CatClipComposer.Core.Models;

namespace CatClipComposer.Core.Services;

public interface IMediaCatalog
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFile>> GetAllAsync(
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default);

    Task<MediaFile> UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);

    Task SetAvailabilityAsync(long id, bool isAvailable, CancellationToken cancellationToken = default);

    Task UpdateTagsAsync(long id, string tags, CancellationToken cancellationToken = default);

    Task MarkSeenAsync(long id, CancellationToken cancellationToken = default);

    Task ReplaceProjectMediaReferencesAsync(
        Guid projectId,
        IReadOnlyCollection<long> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, int>> GetProjectReferenceCountsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaUsageEntry>> GetUsageAsync(
        long mediaFileId,
        CancellationToken cancellationToken = default);

    Task RecordExportAsync(
        string outputPath,
        TimeSpan duration,
        IReadOnlyList<long> mediaFileIds,
        string? projectName = null,
        string? projectFilePath = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExportHistoryEntry>> GetExportHistoryAsync(
        CancellationToken cancellationToken = default);
}
