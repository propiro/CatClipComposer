using System.Globalization;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using CatClipComposer.Infrastructure.Persistence;

namespace CatClipComposer.Infrastructure;

public sealed class SqliteMediaCatalog : IMediaCatalog
{
    private readonly AppPaths _paths;
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteMediaCatalog(AppPaths paths)
    {
        _paths = paths;
        _connectionFactory = new SqliteConnectionFactory(paths);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        await SqliteCatalogSchema.InitializeAsync(_connectionFactory, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaFile>> GetAllAsync(
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        var files = new List<MediaFile>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SqliteMediaMapper.SelectColumns}
            FROM media_files
            {(includeUnavailable ? string.Empty : "WHERE is_available = 1")}
            ORDER BY file_name COLLATE NOCASE, full_path COLLATE NOCASE;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(SqliteMediaMapper.Read(reader));
        }

        return files;
    }

    public async Task<MediaFile> UpsertAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO media_files (
                    full_path, file_name, extension, duration_ticks, width, height, has_audio,
                    file_size, last_write_utc, thumbnail_path, discovered_utc, last_scanned_utc,
                    is_available, preview_sheet_path, tags, is_seen)
                VALUES (
                    $fullPath, $fileName, $extension, $durationTicks, $width, $height, $hasAudio,
                    $fileSize, $lastWriteUtc, $thumbnailPath, $discoveredUtc, $lastScannedUtc, 1,
                    $previewSheetPath, $tags, $isSeen)
                ON CONFLICT(full_path) DO UPDATE SET
                    file_name = excluded.file_name,
                    extension = excluded.extension,
                    duration_ticks = excluded.duration_ticks,
                    width = excluded.width,
                    height = excluded.height,
                    has_audio = excluded.has_audio,
                    file_size = excluded.file_size,
                    last_write_utc = excluded.last_write_utc,
                    thumbnail_path = excluded.thumbnail_path,
                    preview_sheet_path = excluded.preview_sheet_path,
                    last_scanned_utc = excluded.last_scanned_utc,
                    is_available = 1;
                """;
            SqliteMediaMapper.AddUpsertParameters(command, mediaFile);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = $"""
            SELECT {SqliteMediaMapper.SelectColumns}
            FROM media_files
            WHERE full_path = $fullPath;
            """;
        selectCommand.Parameters.AddWithValue("$fullPath", mediaFile.FullPath);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The media catalog did not return the saved file.");
        }

        return SqliteMediaMapper.Read(reader);
    }

    public async Task SetAvailabilityAsync(
        long id,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE media_files SET is_available = $available WHERE id = $id;";
        command.Parameters.AddWithValue("$available", isAvailable ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateTagsAsync(
        long id,
        string tags,
        CancellationToken cancellationToken = default)
    {
        var normalizedTags = string.Join(
            "; ",
            tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE media_files SET tags = $tags WHERE id = $id;";
        command.Parameters.AddWithValue("$tags", normalizedTags);
        command.Parameters.AddWithValue("$id", id);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"Catalog clip {id} was not found.");
        }
    }

    public async Task MarkSeenAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE media_files SET is_seen = 1 WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"Catalog clip {id} was not found.");
        }
    }

    public async Task ReplaceProjectMediaReferencesAsync(
        Guid projectId,
        IReadOnlyCollection<long> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM project_media_references WHERE project_id = $projectId;";
            deleteCommand.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var updatedUtc = SqliteUtc.Format(DateTime.UtcNow);
        foreach (var mediaFileId in mediaFileIds.Where(id => id > 0).Distinct())
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO project_media_references(project_id, media_file_id, updated_utc)
                SELECT $projectId, id, $updatedUtc
                FROM media_files
                WHERE id = $mediaFileId;
                """;
            insertCommand.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$mediaFileId", mediaFileId);
            insertCommand.Parameters.AddWithValue("$updatedUtc", updatedUtc);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, int>> GetProjectReferenceCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<long, int>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT media_file_id, COUNT(*)
            FROM project_media_references
            GROUP BY media_file_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetInt64(0)] = reader.GetInt32(1);
        }

        return counts;
    }

    public async Task<IReadOnlyList<MediaUsageEntry>> GetUsageAsync(
        long mediaFileId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<MediaUsageEntry>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT jobs.id, jobs.project_name, jobs.project_file_path, jobs.output_path,
                   jobs.created_utc, COUNT(*)
            FROM render_job_items AS items
            INNER JOIN render_jobs AS jobs ON jobs.id = items.render_job_id
            WHERE items.media_file_id = $mediaFileId
            GROUP BY jobs.id, jobs.project_name, jobs.project_file_path,
                     jobs.output_path, jobs.created_utc
            ORDER BY jobs.created_utc DESC, jobs.id DESC;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new MediaUsageEntry(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                SqliteUtc.Parse(reader.GetString(4)),
                reader.GetInt32(5)));
        }

        return entries;
    }

    public async Task RecordExportAsync(
        string outputPath,
        TimeSpan duration,
        IReadOnlyList<long> mediaFileIds,
        string? projectName = null,
        string? projectFilePath = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var now = SqliteUtc.Format(DateTime.UtcNow);

        await using var jobCommand = connection.CreateCommand();
        jobCommand.Transaction = transaction;
        jobCommand.CommandText = """
            INSERT INTO render_jobs(
                output_path, duration_ticks, created_utc, project_name, project_file_path)
            VALUES(
                $outputPath, $durationTicks, $createdUtc, $projectName, $projectFilePath);
            SELECT last_insert_rowid();
            """;
        jobCommand.Parameters.AddWithValue("$outputPath", outputPath);
        jobCommand.Parameters.AddWithValue("$durationTicks", duration.Ticks);
        jobCommand.Parameters.AddWithValue("$createdUtc", now);
        jobCommand.Parameters.AddWithValue("$projectName", (object?)projectName ?? DBNull.Value);
        jobCommand.Parameters.AddWithValue("$projectFilePath", (object?)projectFilePath ?? DBNull.Value);
        var jobId = Convert.ToInt64(
            await jobCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        for (var index = 0; index < mediaFileIds.Count; index++)
        {
            await using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = """
                INSERT INTO render_job_items(render_job_id, media_file_id, sort_order)
                VALUES($jobId, $mediaFileId, $sortOrder);

                UPDATE media_files
                SET use_count = use_count + 1,
                    last_used_utc = $usedUtc,
                    last_output_path = $outputPath
                WHERE id = $mediaFileId;
                """;
            itemCommand.Parameters.AddWithValue("$jobId", jobId);
            itemCommand.Parameters.AddWithValue("$mediaFileId", mediaFileIds[index]);
            itemCommand.Parameters.AddWithValue("$sortOrder", index);
            itemCommand.Parameters.AddWithValue("$usedUtc", now);
            itemCommand.Parameters.AddWithValue("$outputPath", outputPath);
            await itemCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportHistoryEntry>> GetExportHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT jobs.id, jobs.output_path, jobs.duration_ticks, jobs.created_utc,
                   items.sort_order, media.id, media.file_name, media.full_path,
                   jobs.project_name, jobs.project_file_path
            FROM render_jobs AS jobs
            LEFT JOIN render_job_items AS items ON items.render_job_id = jobs.id
            LEFT JOIN media_files AS media ON media.id = items.media_file_id
            ORDER BY jobs.created_utc DESC, jobs.id DESC, items.sort_order ASC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await SqliteExportHistoryReader.ReadAllAsync(reader, cancellationToken);
    }
}
