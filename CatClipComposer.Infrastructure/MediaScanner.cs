using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure;

public sealed class MediaScanner(
    IMediaCatalog catalog,
    IMediaProbe mediaProbe,
    IThumbnailGenerator thumbnailGenerator,
    IPreviewSheetGenerator previewSheetGenerator) : IMediaScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".mp4", ".webm", ".avi", ".mov", ".mkv", ".m4v"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<ScanResult> ScanAsync(
        ApplicationSettings settings,
        ScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var errors = new List<string>();
        var roots = NormalizeRoots(settings.SourceFolders, errors);
        var files = EnumerateFiles(roots, settings.IncludeSubfolders, errors);
        var existingFiles = await catalog.GetAllAsync(
            includeUnavailable: true,
            cancellationToken);
        var existingByPath = existingFiles.ToDictionary(
            file => file.FullPath,
            StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var updated = 0;
        var failed = 0;

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = files[index];
            seenPaths.Add(filePath);
            progress?.Report(new ScanProgress(
                index,
                files.Count,
                added,
                updated,
                failed,
                Path.GetFileName(filePath)));

            try
            {
                var info = new FileInfo(filePath);
                existingByPath.TryGetValue(filePath, out var existing);
                VideoMetadata metadata;
                string? thumbnailPath;
                string? previewSheetPath;

                if (!options.RegeneratePreviews &&
                    existing is not null &&
                    existing.FileSize == info.Length &&
                    existing.LastWriteUtc == info.LastWriteTimeUtc &&
                    existing.Duration > TimeSpan.Zero)
                {
                    metadata = new VideoMetadata(
                        existing.Duration,
                        existing.Width,
                        existing.Height,
                        existing.HasAudio);
                    thumbnailPath = existing.ThumbnailPath;
                }
                else
                {
                    metadata = await mediaProbe.ProbeAsync(
                        filePath,
                        settings.FfmpegPath,
                        cancellationToken);
                    thumbnailPath = await thumbnailGenerator.CreateAsync(
                        filePath,
                        metadata.Duration,
                        info.LastWriteTimeUtc,
                        settings.FfmpegPath,
                        options.RegeneratePreviews,
                        cancellationToken);
                }

                previewSheetPath = await previewSheetGenerator.CreateAsync(
                    filePath,
                    metadata.Duration,
                    info.LastWriteTimeUtc,
                    settings.PreviewSlideCount,
                    settings.FfmpegPath,
                    options.RegeneratePreviews,
                    cancellationToken);

                var now = DateTime.UtcNow;
                await catalog.UpsertAsync(new MediaFile
                {
                    Id = existing?.Id ?? 0,
                    FullPath = info.FullName,
                    FileName = info.Name,
                    Extension = info.Extension.ToLowerInvariant(),
                    DurationTicks = metadata.Duration.Ticks,
                    Width = metadata.Width,
                    Height = metadata.Height,
                    HasAudio = metadata.HasAudio,
                    FileSize = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc,
                    ThumbnailPath = thumbnailPath,
                    PreviewSheetPath = previewSheetPath,
                    Tags = existing?.Tags ?? string.Empty,
                    DiscoveredUtc = existing?.DiscoveredUtc ?? now,
                    LastScannedUtc = now,
                    IsAvailable = true,
                    IsSeen = existing?.IsSeen ?? false,
                    ProjectReferenceCount = existing?.ProjectReferenceCount ?? 0,
                    UseCount = existing?.UseCount ?? 0,
                    LastUsedUtc = existing?.LastUsedUtc,
                    LastOutputPath = existing?.LastOutputPath
                }, cancellationToken);

                if (existing is null)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                errors.Add($"{filePath}: {exception.Message}");
            }
        }

        foreach (var existing in existingFiles.Where(file => file.IsAvailable))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsWithinAnyRoot(existing.FullPath, roots) &&
                !seenPaths.Contains(existing.FullPath) &&
                !File.Exists(existing.FullPath))
            {
                await catalog.SetAvailabilityAsync(existing.Id, false, cancellationToken);
            }
        }

        progress?.Report(new ScanProgress(
            files.Count,
            files.Count,
            added,
            updated,
            failed,
            string.Empty));
        return new ScanResult(files.Count, added, updated, failed, errors);
    }

    private static List<string> NormalizeRoots(
        IEnumerable<string> sourceFolders,
        ICollection<string> errors)
    {
        var roots = new List<string>();
        foreach (var sourceFolder in sourceFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var fullPath = Path.GetFullPath(sourceFolder);
                if (Directory.Exists(fullPath))
                {
                    roots.Add(fullPath);
                }
                else
                {
                    errors.Add($"Source folder does not exist: {sourceFolder}");
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"Invalid source folder '{sourceFolder}': {exception.Message}");
            }
        }

        return roots;
    }

    private static List<string> EnumerateFiles(
        IEnumerable<string> roots,
        bool includeSubfolders,
        ICollection<string> errors)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

        foreach (var root in roots)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", options))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        files.Add(Path.GetFullPath(file));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not finish scanning '{root}': {exception.Message}");
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsWithinAnyRoot(string filePath, IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root, filePath);
            if (!relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative))
            {
                return true;
            }
        }

        return false;
    }
}
