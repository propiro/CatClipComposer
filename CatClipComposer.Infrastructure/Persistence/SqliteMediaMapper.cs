using CatClipComposer.Core.Models;
using Microsoft.Data.Sqlite;

namespace CatClipComposer.Infrastructure.Persistence;

internal static class SqliteMediaMapper
{
    public const string SelectColumns = """
        id, full_path, file_name, extension, duration_ticks, width, height,
        has_audio, file_size, last_write_utc, thumbnail_path, discovered_utc,
        last_scanned_utc, is_available, use_count, last_used_utc, last_output_path
        """;

    public static void AddUpsertParameters(SqliteCommand command, MediaFile mediaFile)
    {
        command.Parameters.AddWithValue("$fullPath", mediaFile.FullPath);
        command.Parameters.AddWithValue("$fileName", mediaFile.FileName);
        command.Parameters.AddWithValue("$extension", mediaFile.Extension);
        command.Parameters.AddWithValue("$durationTicks", mediaFile.DurationTicks);
        command.Parameters.AddWithValue("$width", mediaFile.Width);
        command.Parameters.AddWithValue("$height", mediaFile.Height);
        command.Parameters.AddWithValue("$hasAudio", mediaFile.HasAudio ? 1 : 0);
        command.Parameters.AddWithValue("$fileSize", mediaFile.FileSize);
        command.Parameters.AddWithValue("$lastWriteUtc", SqliteUtc.Format(mediaFile.LastWriteUtc));
        command.Parameters.AddWithValue("$thumbnailPath", (object?)mediaFile.ThumbnailPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$discoveredUtc", SqliteUtc.Format(mediaFile.DiscoveredUtc));
        command.Parameters.AddWithValue("$lastScannedUtc", SqliteUtc.Format(mediaFile.LastScannedUtc));
    }

    public static MediaFile Read(SqliteDataReader reader) => new()
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
        LastWriteUtc = SqliteUtc.Parse(reader.GetString(9)),
        ThumbnailPath = reader.IsDBNull(10) ? null : reader.GetString(10),
        DiscoveredUtc = SqliteUtc.Parse(reader.GetString(11)),
        LastScannedUtc = SqliteUtc.Parse(reader.GetString(12)),
        IsAvailable = reader.GetInt64(13) != 0,
        UseCount = reader.GetInt32(14),
        LastUsedUtc = reader.IsDBNull(15) ? null : SqliteUtc.Parse(reader.GetString(15)),
        LastOutputPath = reader.IsDBNull(16) ? null : reader.GetString(16)
    };
}
