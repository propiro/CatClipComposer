using Microsoft.Data.Sqlite;

namespace CatClipComposer.Infrastructure.Persistence;

internal static class SqliteCatalogSchema
{
    public static async Task InitializeAsync(
        SqliteConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
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
                created_utc TEXT NOT NULL,
                project_name TEXT NULL,
                project_file_path TEXT NULL
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
        await EnsureColumnAsync(
            connection,
            "render_jobs",
            "project_name",
            "TEXT NULL",
            cancellationToken);
        await EnsureColumnAsync(
            connection,
            "render_jobs",
            "project_file_path",
            "TEXT NULL",
            cancellationToken);
    }

    public static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string declaration,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var inspectCommand = connection.CreateCommand())
        {
            inspectCommand.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await inspectCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (columns.Contains(column))
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
