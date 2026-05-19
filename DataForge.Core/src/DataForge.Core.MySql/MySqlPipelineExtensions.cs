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

    public static Task<ExportResults> ToMySql<T>(this IDataPipeline<T> pipeline, string connectionString, string tableName, MySqlExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new MySqlTarget<T>(options ?? new MySqlExportOptions());
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), $"{connectionString}|{tableName}", cancellationToken);
    }
}
