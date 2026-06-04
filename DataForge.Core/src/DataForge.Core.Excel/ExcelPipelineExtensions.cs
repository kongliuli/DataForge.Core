using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Excel;

public static class ExcelPipelineExtensions
{
    public static IDataPipeline<T> FromExcel<T>(this DataForgePipeline builder, string filePath, ExcelSourceOptions? options = null) where T : new()
    {
        var source = new ExcelSource<T>(filePath, options);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static Task<ExportResults> ToExcel<T>(this IDataPipeline<T> pipeline, string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new ExcelTarget<T>(options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), filePath, cancellationToken);
    }
}
