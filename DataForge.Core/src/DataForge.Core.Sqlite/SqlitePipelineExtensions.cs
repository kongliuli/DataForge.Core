using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Sqlite;

public static class SqlitePipelineExtensions
{
    public static IDataPipeline<T> FromSqlite<T>(string connectionString, string tableName) where T : new()
    {
        var source = new SqliteSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static Task<ExportResults> ToSqlite<T>(this IDataPipeline<T> pipeline, string connectionString, string tableName, SqliteExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new SqliteTarget<T>(options ?? new SqliteExportOptions());
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), $"{connectionString}|{tableName}", cancellationToken);
    }
}
