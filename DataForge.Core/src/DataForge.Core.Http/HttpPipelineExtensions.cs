using DataForge.Core;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Http;

public static class HttpPipelineExtensions
{
    public static IDataPipeline<T> FromRestApi<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null)
    {
        var sourceOptions = options ?? new RestApiSourceOptions
        {
            BaseUrl = baseUrl,
            Endpoint = endpoint
        };
        var source = new RestApiSource<T>(null, sourceOptions);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromRestApi<T>(HttpClient httpClient, string endpoint, RestApiSourceOptions? options = null)
    {
        var sourceOptions = options ?? new RestApiSourceOptions { Endpoint = endpoint };
        var source = new RestApiSource<T>(httpClient, sourceOptions);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromRestApi<T>(this DataForgePipeline _, string baseUrl, string endpoint, RestApiSourceOptions? options = null)
        => FromRestApi<T>(baseUrl, endpoint, options);

    public static IDataPipeline<T> FromRestApiWithPagination<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null)
    {
        var sourceOptions = options ?? new RestApiSourceOptions
        {
            BaseUrl = baseUrl,
            Endpoint = endpoint
        };
        var source = new RestApiSource<T>(null, sourceOptions);
        return new DataPipeline<T>(source.ReadWithPaginationAsync());
    }

    public static Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, string baseUrl, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
    {
        var targetOptions = options ?? new RestApiTargetOptions
        {
            BaseUrl = baseUrl,
            Endpoint = endpoint
        };
        var target = new RestApiTarget<T>(null, targetOptions);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), $"{baseUrl}{endpoint}", cancellationToken);
    }

    public static Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, HttpClient httpClient, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new RestApiTarget<T>(httpClient, options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), endpoint, cancellationToken);
    }
}
