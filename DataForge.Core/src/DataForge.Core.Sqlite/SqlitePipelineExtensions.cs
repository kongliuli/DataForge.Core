using DataForge.Core.Core.Pipeline;

namespace DataForge.Core.Sqlite;

public static class SqlitePipelineExtensions
{
    public static IDataPipeline<T> FromSqlite<T>(string connectionString, string tableName) where T : new()
    {
        var source = new SqliteSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }
}