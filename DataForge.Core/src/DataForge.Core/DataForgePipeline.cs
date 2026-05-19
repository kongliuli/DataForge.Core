using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Sources;
using DataForge.Core.Core.Sources.Implementations;
using DataForge.Core.Core.Sources.Options;
using System.Collections.Generic;

namespace DataForge.Core;

public static class DataForgePipeline
{
    public static IDataPipeline<T> FromCsv<T>(string filePath, CsvSourceOptions? options = null)
    {
        var source = new CsvSource<T>(filePath, options);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromJson<T>(string filePath, JsonSourceOptions? options = null)
    {
        var source = new JsonSource<T>(filePath, options ?? new JsonSourceOptions());
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromMemory<T>(IEnumerable<T> data)
    {
        var source = new MemorySource<T>(data);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromExcel<T>(string filePath, string sheetName = "Sheet1")
    {
        var source = new ExcelSource<T>(filePath, sheetName);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> Merge<T>(params IDataSource<T>[] sources)
    {
        return new DataPipeline<T>(MergeSources(sources));
    }

    private static async IAsyncEnumerable<T> MergeSources<T>(params IDataSource<T>[] sources)
    {
        foreach (var source in sources)
        {
            await foreach (var item in source.ReadAsync().ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }
}