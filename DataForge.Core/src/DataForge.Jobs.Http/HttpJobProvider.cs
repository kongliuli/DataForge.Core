using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Http;

public static class JobHttpRegistration
{
    public static void AddDataForgeJobsHttp()
    {
        var provider = new HttpJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
    }
}

internal sealed class HttpJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["http", "rest"];
    public IReadOnlyList<string> SinkTypes { get; } = ["http", "rest"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        var url = job.Source.Path;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("HTTP source requires path (URL).");
        return new DataPipeline<JobRow>(ReadAsync(url));
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        PostAsync(rows, sink, cancellationToken);

    private static async IAsyncEnumerable<JobRow> ReadAsync(
        string url,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new JobRow();
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var prop in item.EnumerateObject())
                row.Values[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
            yield return row;
        }
    }

    private static async Task<ExportResults> PostAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sink.Path))
            throw new InvalidOperationException("HTTP sink requires path (URL).");

        var payload = new List<Dictionary<string, string?>>();
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            payload.Add(new Dictionary<string, string?>(row.Values, StringComparer.OrdinalIgnoreCase));

        var sw = Stopwatch.StartNew();
        using var client = new HttpClient();
        using var response = await client.PostAsJsonAsync(sink.Path, payload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        sw.Stop();
        return new ExportResults { RecordsWritten = payload.Count, Duration = sw.Elapsed, OutputPath = sink.Path };
    }
}
