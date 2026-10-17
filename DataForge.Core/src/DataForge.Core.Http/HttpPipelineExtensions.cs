using DataForge.Core;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Http;

public static class HttpPipelineExtensions
{
    public static IDataPipeline<T> FromRestApi<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null)
    {
        var sourceOptions = options ?? new RestApiSourceOptions();
        sourceOptions.BaseUrl = string.IsNullOrWhiteSpace(sourceOptions.BaseUrl) ? baseUrl : sourceOptions.BaseUrl;
        sourceOptions.Endpoint = string.IsNullOrWhiteSpace(sourceOptions.Endpoint) ? endpoint : sourceOptions.Endpoint;
        var source = new RestApiSource<T>(null, sourceOptions);
        return new DataPipeline<T>(EnumerateAndDispose(source, paginated: false));
    }

    public static IDataPipeline<T> FromRestApi<T>(HttpClient httpClient, string endpoint, RestApiSourceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var sourceOptions = options ?? new RestApiSourceOptions { Endpoint = endpoint };
        if (string.IsNullOrWhiteSpace(sourceOptions.Endpoint))
        {
            sourceOptions.Endpoint = endpoint;
        }

        var source = new RestApiSource<T>(httpClient, sourceOptions);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromRestApi<T>(this DataForgePipeline _, string baseUrl, string endpoint, RestApiSourceOptions? options = null)
        => FromRestApi<T>(baseUrl, endpoint, options);

    public static IDataPipeline<T> FromRestApi<T>(this DataForgePipeline _, HttpClient httpClient, string endpoint, RestApiSourceOptions? options = null)
        => FromRestApi<T>(httpClient, endpoint, options);

    public static IDataPipeline<T> FromRestApiWithPagination<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null)
    {
        var sourceOptions = options ?? new RestApiSourceOptions();
        sourceOptions.BaseUrl = string.IsNullOrWhiteSpace(sourceOptions.BaseUrl) ? baseUrl : sourceOptions.BaseUrl;
        sourceOptions.Endpoint = string.IsNullOrWhiteSpace(sourceOptions.Endpoint) ? endpoint : sourceOptions.Endpoint;
        var source = new RestApiSource<T>(null, sourceOptions);
        return new DataPipeline<T>(EnumerateAndDispose(source, paginated: true));
    }

    public static IDataPipeline<T> FromRestApiWithPagination<T>(this DataForgePipeline _, string baseUrl, string endpoint, RestApiSourceOptions? options = null)
        => FromRestApiWithPagination<T>(baseUrl, endpoint, options);

    public static async Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, string baseUrl, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
    {
        var targetOptions = options ?? new RestApiTargetOptions();
        targetOptions.BaseUrl = string.IsNullOrWhiteSpace(targetOptions.BaseUrl) ? baseUrl : targetOptions.BaseUrl;
        targetOptions.Endpoint = string.IsNullOrWhiteSpace(targetOptions.Endpoint) ? endpoint : targetOptions.Endpoint;
        using var target = new RestApiTarget<T>(null, targetOptions);
        return await target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), $"{baseUrl}{endpoint}", cancellationToken).ConfigureAwait(false);
    }

    public static Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, HttpClient httpClient, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var targetOptions = options ?? new RestApiTargetOptions { Endpoint = endpoint };
        if (string.IsNullOrWhiteSpace(targetOptions.Endpoint))
        {
            targetOptions.Endpoint = endpoint;
        }

        var target = new RestApiTarget<T>(httpClient, targetOptions);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), endpoint, cancellationToken);
    }

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToRestApiAsync<T>(this IDataPipeline<T> pipeline, string baseUrl, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
        => ToRestApi(pipeline, baseUrl, endpoint, options, cancellationToken);

    /// <summary>Alias aligned with core <c>To*Async</c> naming.</summary>
    public static Task<ExportResults> ToRestApiAsync<T>(this IDataPipeline<T> pipeline, HttpClient httpClient, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default)
        => ToRestApi(pipeline, httpClient, endpoint, options, cancellationToken);

    private static async IAsyncEnumerable<T> EnumerateAndDispose<T>(RestApiSource<T> source, bool paginated, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            var enumerable = paginated ? source.ReadWithPaginationAsync(cancellationToken) : source.ReadAsync(cancellationToken);
            await foreach (var item in enumerable.ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            source.Dispose();
        }
    }
}
