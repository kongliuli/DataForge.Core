using ClosedXML.Excel;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DataForge.Jobs.Excel;

public static class JobExcelRegistration
{
    public static void AddDataForgeJobsExcel()
    {
        var provider = new ExcelJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new ExcelCatalogProvider());
    }
}

internal sealed class ExcelJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["excel"];
    public IReadOnlyList<string> SinkTypes { get; } = ["excel"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        if (string.IsNullOrWhiteSpace(job.Source.Path))
            throw new InvalidOperationException("Excel source requires path.");
        return new DataPipeline<JobRow>(ReadAsync(job.Source.Path));
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        WriteFileAsync(rows, sink, cancellationToken);

    private static async IAsyncEnumerable<JobRow> ReadAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet(1);
        var range = sheet.RangeUsed();
        if (range == null)
            yield break;

        var headers = new List<string>();
        for (var col = 1; col <= range.ColumnCount(); col++)
            headers.Add(sheet.Cell(1, col).GetString());

        for (var rowIndex = 2; rowIndex <= range.RowCount(); rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new JobRow();
            for (var col = 0; col < headers.Count; col++)
            {
                var text = sheet.Cell(rowIndex, col + 1).GetString();
                row.Values[headers[col]] = string.IsNullOrEmpty(text) ? null : text;
            }
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async Task<ExportResults> WriteFileAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sink.Path))
            throw new InvalidOperationException("Excel sink requires path.");

        var list = new List<JobRow>();
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            list.Add(row);

        var sw = Stopwatch.StartNew();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        var headers = list.Count == 0 ? [] : list[0].Values.Keys.ToArray();
        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];
        for (var i = 0; i < list.Count; i++)
        {
            for (var col = 0; col < headers.Length; col++)
                sheet.Cell(i + 2, col + 1).Value = list[i].Get(headers[col]) ?? "";
        }

        // Match the csv/json/parquet sinks: the target directory need not pre-exist.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sink.Path))!);
        workbook.SaveAs(sink.Path);
        sw.Stop();
        return new ExportResults { RecordsWritten = list.Count, Duration = sw.Elapsed, OutputPath = sink.Path };
    }
}
