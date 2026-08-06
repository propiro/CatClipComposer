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
                    is_available)
                VALUES (
                    $fullPath, $fileName, $extension, $durationTicks, $width, $height, $hasAudio,
                    $fileSize, $lastWriteUtc, $thumbnailPath, $discoveredUtc, $lastScannedUtc, 1)
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

    public async Task RecordExportAsync(
        string outputPath,
        TimeSpan duration,
        IReadOnlyList<long> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var now = SqliteUtc.Format(DateTime.UtcNow);

        await using var jobCommand = connection.CreateCommand();
        jobCommand.Transaction = transaction;
        jobCommand.CommandText = """
            INSERT INTO render_jobs(output_path, duration_ticks, created_utc)
            VALUES($outputPath, $durationTicks, $createdUtc);
            SELECT last_insert_rowid();
            """;
        jobCommand.Parameters.AddWithValue("$outputPath", outputPath);
        jobCommand.Parameters.AddWithValue("$durationTicks", duration.Ticks);
        jobCommand.Parameters.AddWithValue("$createdUtc", now);
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
                   items.sort_order, media.id, media.file_name, media.full_path
            FROM render_jobs AS jobs
            LEFT JOIN render_job_items AS items ON items.render_job_id = jobs.id
            LEFT JOIN media_files AS media ON media.id = items.media_file_id
            ORDER BY jobs.created_utc DESC, jobs.id DESC, items.sort_order ASC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await SqliteExportHistoryReader.ReadAllAsync(reader, cancellationToken);
    }
}
