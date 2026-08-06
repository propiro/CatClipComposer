using CatClipComposer.Core.Models;
using Microsoft.Data.Sqlite;

namespace CatClipComposer.Infrastructure.Persistence;

internal static class SqliteExportHistoryReader
{
    public static async Task<IReadOnlyList<ExportHistoryEntry>> ReadAllAsync(
        SqliteDataReader reader,
        CancellationToken cancellationToken)
    {
        var entries = new List<ExportHistoryEntry>();
        ExportJobBuilder? current = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var jobId = reader.GetInt64(0);
            if (current is not null && current.Id != jobId)
            {
                entries.Add(current.Build());
                current = null;
            }

            current ??= new ExportJobBuilder(
                jobId,
                reader.GetString(1),
                TimeSpan.FromTicks(reader.GetInt64(2)),
                SqliteUtc.Parse(reader.GetString(3)),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9));
            if (!reader.IsDBNull(4))
            {
                current.Clips.Add(new ExportHistoryClip(
                    reader.GetInt32(4) + 1,
                    reader.GetInt64(5),
                    reader.GetString(6),
                    reader.GetString(7)));
            }
        }

        if (current is not null)
        {
            entries.Add(current.Build());
        }

        return entries;
    }

    private sealed record ExportJobBuilder(
        long Id,
        string OutputPath,
        TimeSpan Duration,
        DateTime CreatedUtc,
        string? ProjectName,
        string? ProjectFilePath)
    {
        public List<ExportHistoryClip> Clips { get; } = [];

        public ExportHistoryEntry Build() => new(
            Id,
            OutputPath,
            Duration,
            CreatedUtc,
            Clips.ToList(),
            ProjectName,
            ProjectFilePath);
    }
}
