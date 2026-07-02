using CsvHelper;
using CsvHelper.Configuration;
using DataMigration.Contracts;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace DataMigration.Plugin.CsvSource;

public class CsvSource : IDataSource
{
    public string Id => "DataMigration.Plugin.CsvSource";
    public string Name => "CSV 数据源";
    public Version Version => new Version(1, 0, 0);

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!config.TryGetValue("FilePath", out var filePath) || string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("CSV 文件路径未配置");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV 文件不存在", filePath);
        }

        var delimiter = config.TryGetValue("Delimiter", out var d) ? d : ",";
        var hasHeader = config.TryGetValue("HasHeader", out var h) ? bool.Parse(h) : true;

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = hasHeader
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, csvConfig);

        await csv.ReadAsync().ConfigureAwait(false);
        if (hasHeader)
        {
            csv.ReadHeader();
        }

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var record = new DataRecord();
            var headerNames = csv.HeaderRecord ?? Enumerable.Range(0, csv.ColumnCount).Select(i => $"Column{i}");

            foreach (var header in headerNames)
            {
                var value = csv.GetField(header);
                record.SetValue(header, value);
            }

            yield return record;
        }
    }
}