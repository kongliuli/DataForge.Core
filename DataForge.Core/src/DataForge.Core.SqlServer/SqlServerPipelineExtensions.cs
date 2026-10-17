using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.SqlServer;

public static class SqlServerPipelineExtensions
{
    public static IDataPipeline<T> FromSqlServer<T>(string connectionString, string tableName) where T : new()
    {
        var source = new SqlServerSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }

    /// <summary>Builder-style alias for <see cref="FromSqlServer{T}(string, string)"/>.</summary>
    public static IDataPipeline<T> FromSqlServer<T>(this DataForgePipeline builder, string connectionString, string tableName) where T : new()
        => FromSqlServer<T>(connectionString, tableName);

    public static Task<ExportResults> ToSqlServer<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        SqlServerExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = new SqlServerTarget<T>(connectionString, tableName, options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), tableName, cancellationToken);
    }

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToSqlServerAsync<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        SqlServerExportOptions? options = null,
        CancellationToken cancellationToken = default)
        => ToSqlServer(pipeline, connectionString, tableName, options, cancellationToken);
}
