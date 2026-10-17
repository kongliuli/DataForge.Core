using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.MySql;

public static class MySqlPipelineExtensions
{
    public static IDataPipeline<T> FromMySql<T>(string connectionString, string tableName) where T : new()
    {
        var source = new MySqlSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }

    /// <summary>Builder-style alias for <see cref="FromMySql{T}(string, string)"/>.</summary>
    public static IDataPipeline<T> FromMySql<T>(this DataForgePipeline builder, string connectionString, string tableName) where T : new()
        => FromMySql<T>(connectionString, tableName);

    public static Task<ExportResults> ToMySql<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        MySqlExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = new MySqlTarget<T>(connectionString, tableName, options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), tableName, cancellationToken);
    }

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToMySqlAsync<T>(
        this IDataPipeline<T> pipeline,
        string connectionString,
        string tableName,
        MySqlExportOptions? options = null,
        CancellationToken cancellationToken = default)
        => ToMySql(pipeline, connectionString, tableName, options, cancellationToken);
}
