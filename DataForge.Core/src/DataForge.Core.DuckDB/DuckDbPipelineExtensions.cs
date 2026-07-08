using DataForge.Core;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.DuckDB;

public static class DuckDbPipelineExtensions
{
    public static IDataPipeline<T> FromDuckDb<T>(string databasePath, string sql, DuckDbSourceOptions? options = null)
        where T : new()
    {
        var source = new DuckDbSource<T>(databasePath, sql, options);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromDuckDb<T>(
        this DataForgePipeline _,
        string databasePath,
        string sql,
        DuckDbSourceOptions? options = null)
        where T : new()
        => FromDuckDb<T>(databasePath, sql, options);

    public static Task<ExportResults> ToDuckDbAsync<T>(
        this IDataPipeline<T> pipeline,
        string databasePath,
        string tableName,
        DuckDbExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = new DuckDbTarget<T>(options);
        return target.ExportAsync(
            pipeline.AsAsyncEnumerable(cancellationToken),
            $"{databasePath}|{tableName}",
            cancellationToken);
    }
}
