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

    /// <summary>Builder-style alias for <see cref="FromSqlite{T}(string, string)"/>.</summary>
    public static IDataPipeline<T> FromSqlite<T>(this DataForgePipeline builder, string connectionString, string tableName) where T : new()
        => FromSqlite<T>(connectionString, tableName);

    public static Task<ExportResults> ToSqlite<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        SqliteExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = new SqliteTarget<T>(connectionString, tableName, options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), tableName, cancellationToken);
    }

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToSqliteAsync<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        SqliteExportOptions? options = null,
        CancellationToken cancellationToken = default)
        => ToSqlite(pipeline, connectionString, tableName, options, cancellationToken);
}
