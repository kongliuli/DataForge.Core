using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.PostgreSql;

public static class PostgreSqlPipelineExtensions
{
    public static IDataPipeline<T> FromPostgreSql<T>(string connectionString, string tableName) where T : new()
    {
        var source = new PostgreSqlSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }

    /// <summary>Builder-style alias for <see cref="FromPostgreSql{T}(string, string)"/>.</summary>
    public static IDataPipeline<T> FromPostgreSql<T>(this DataForgePipeline builder, string connectionString, string tableName) where T : new()
        => FromPostgreSql<T>(connectionString, tableName);

    public static Task<ExportResults> ToPostgreSql<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        PostgreSqlExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = new PostgreSqlTarget<T>(connectionString, tableName, options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), tableName, cancellationToken);
    }

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToPostgreSqlAsync<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        PostgreSqlExportOptions? options = null,
        CancellationToken cancellationToken = default)
        => ToPostgreSql(pipeline, connectionString, tableName, options, cancellationToken);
}
