using Microsoft.Data.Sqlite;

namespace CatClipComposer.Infrastructure.Persistence;

internal sealed class SqliteConnectionFactory(AppPaths paths)
{
    public SqliteConnection Create()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        return new SqliteConnection(builder.ToString());
    }
}
