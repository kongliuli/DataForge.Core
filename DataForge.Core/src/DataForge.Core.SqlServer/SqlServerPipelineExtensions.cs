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

    public static Task<ExportResults> ToSqlServer<T>(this IDataPipeline<T> pipeline, string connectionString, string tableName, SqlServerExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new SqlServerTarget<T>(options ?? new SqlServerExportOptions());
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), $"{connectionString}|{tableName}", cancellationToken);
    }
}
