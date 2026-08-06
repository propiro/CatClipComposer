using System.Globalization;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;
using Microsoft.Data.Sqlite;

namespace CatClipComposer.Infrastructure;

public sealed class SqliteMediaCatalog(AppPaths paths) : IMediaCatalog
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS media_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                full_path TEXT NOT NULL COLLATE NOCASE UNIQUE,
                file_name TEXT NOT NULL,
                extension TEXT NOT NULL,
                duration_ticks INTEGER NOT NULL,
                width INTEGER NOT NULL,
                height INTEGER NOT NULL,
                has_audio INTEGER NOT NULL DEFAULT 0,
                file_size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                thumbnail_path TEXT NULL,
                discovered_utc TEXT NOT NULL,
                last_scanned_utc TEXT NOT NULL,
                is_available INTEGER NOT NULL DEFAULT 1,
                use_count INTEGER NOT NULL DEFAULT 0,
                last_used_utc TEXT NULL,
                last_output_path TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_media_files_available_name
            ON media_files(is_available, file_name);

            CREATE TABLE IF NOT EXISTS render_jobs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                output_path TEXT NOT NULL,
                duration_ticks INTEGER NOT NULL,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS render_job_items (
                render_job_id INTEGER NOT NULL,
                media_file_id INTEGER NOT NULL,
                sort_order INTEGER NOT NULL,
                PRIMARY KEY(render_job_id, sort_order),
                FOREIGN KEY(render_job_id) REFERENCES render_jobs(id) ON DELETE CASCADE,
                FOREIGN KEY(media_file_id) REFERENCES media_files(id)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MediaFile>> GetAllAsync(
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        var files = new List<MediaFile>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, full_path, file_name, extension, duration_ticks, width, height,
                   has_audio, file_size, last_write_utc, thumbnail_path, discovered_utc,
                   last_scanned_utc, is_available, use_count, last_used_utc, last_output_path
            FROM media_files
            {(includeUnavailable ? string.Empty : "WHERE is_available = 1")}
            ORDER BY file_name COLLATE NOCASE, full_path COLLATE NOCASE;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(ReadMediaFile(reader));
        }

        return files;
    }

    public async Task<MediaFile> UpsertAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
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
            AddMediaParameters(command, mediaFile);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = """
            SELECT id, full_path, file_name, extension, duration_ticks, width, height,
                   has_audio, file_size, last_write_utc, thumbnail_path, discovered_utc,
                   last_scanned_utc, is_available, use_count, last_used_utc, last_output_path
            FROM media_files
            WHERE full_path = $fullPath;
            """;
        selectCommand.Parameters.AddWithValue("$fullPath", mediaFile.FullPath);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The media catalog did not return the saved file.");
        }

        return ReadMediaFile(reader);
    }

    public async Task SetAvailabilityAsync(
        long id,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
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
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

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
        var entries = new List<ExportHistoryEntry>();
        await using var connection = CreateConnection();
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

        long? currentId = null;
        string currentOutputPath = string.Empty;
        TimeSpan currentDuration = TimeSpan.Zero;
        DateTime currentCreatedUtc = default;
        var clips = new List<ExportHistoryClip>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var jobId = reader.GetInt64(0);
            if (currentId.HasValue && currentId.Value != jobId)
            {
                entries.Add(new ExportHistoryEntry(
                    currentId.Value,
                    currentOutputPath,
                    currentDuration,
                    currentCreatedUtc,
                    clips.ToList()));
                clips.Clear();
            }

            if (currentId != jobId)
            {
                currentId = jobId;
                currentOutputPath = reader.GetString(1);
                currentDuration = TimeSpan.FromTicks(reader.GetInt64(2));
                currentCreatedUtc = ParseUtc(reader.GetString(3));
            }

            if (!reader.IsDBNull(4))
            {
                clips.Add(new ExportHistoryClip(
                    reader.GetInt32(4) + 1,
                    reader.GetInt64(5),
                    reader.GetString(6),
                    reader.GetString(7)));
            }
        }

        if (currentId.HasValue)
        {
            entries.Add(new ExportHistoryEntry(
                currentId.Value,
                currentOutputPath,
                currentDuration,
                currentCreatedUtc,
                clips.ToList()));
        }

        return entries;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        return new SqliteConnection(builder.ToString());
    }

    private static void AddMediaParameters(SqliteCommand command, MediaFile mediaFile)
    {
        command.Parameters.AddWithValue("$fullPath", mediaFile.FullPath);
        command.Parameters.AddWithValue("$fileName", mediaFile.FileName);
        command.Parameters.AddWithValue("$extension", mediaFile.Extension);
        command.Parameters.AddWithValue("$durationTicks", mediaFile.DurationTicks);
        command.Parameters.AddWithValue("$width", mediaFile.Width);
        command.Parameters.AddWithValue("$height", mediaFile.Height);
        command.Parameters.AddWithValue("$hasAudio", mediaFile.HasAudio ? 1 : 0);
        command.Parameters.AddWithValue("$fileSize", mediaFile.FileSize);
        command.Parameters.AddWithValue("$lastWriteUtc", mediaFile.LastWriteUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$thumbnailPath", (object?)mediaFile.ThumbnailPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$discoveredUtc", mediaFile.DiscoveredUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lastScannedUtc", mediaFile.LastScannedUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static MediaFile ReadMediaFile(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        FullPath = reader.GetString(1),
        FileName = reader.GetString(2),
        Extension = reader.GetString(3),
        DurationTicks = reader.GetInt64(4),
        Width = reader.GetInt32(5),
        Height = reader.GetInt32(6),
        HasAudio = reader.GetInt64(7) != 0,
        FileSize = reader.GetInt64(8),
        LastWriteUtc = ParseUtc(reader.GetString(9)),
        ThumbnailPath = reader.IsDBNull(10) ? null : reader.GetString(10),
        DiscoveredUtc = ParseUtc(reader.GetString(11)),
        LastScannedUtc = ParseUtc(reader.GetString(12)),
        IsAvailable = reader.GetInt64(13) != 0,
        UseCount = reader.GetInt32(14),
        LastUsedUtc = reader.IsDBNull(15) ? null : ParseUtc(reader.GetString(15)),
        LastOutputPath = reader.IsDBNull(16) ? null : reader.GetString(16)
    };

    private static DateTime ParseUtc(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
