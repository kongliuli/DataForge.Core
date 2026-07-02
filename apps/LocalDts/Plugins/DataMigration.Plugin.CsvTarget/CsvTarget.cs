using DataMigration.Contracts;
using System.IO;
using System.Runtime.CompilerServices;

namespace DataMigration.Plugin.CsvTarget;

public class CsvTarget : IBatchDataTarget
{
    public string Id => "Standard.CsvTarget";
    public string Name => "CSV File Target";
    public Version Version => new(1, 0, 0);

    private StreamWriter _writer = null!;
    private string _filePath = "";

    public async Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        CancellationToken ct)
    {
        _filePath = config["FilePath"];

        // 确保输出目录存在
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(_filePath);

        bool headerWritten = false;
        await foreach (var record in input.WithCancellation(ct))
        {
            if (!headerWritten)
            {
                var headers = record.Keys;
                await _writer.WriteLineAsync(string.Join(",", headers));
                headerWritten = true;
            }
            var line = string.Join(",", record.Values.Select(v => $"\"{v}\""));
            await _writer.WriteLineAsync(line);
        }
        await _writer.FlushAsync();
    }

    public async Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        int batchSize,
        CancellationToken ct)
    {
        // 对于CSV目标，批处理大小不影响实现，直接调用普通的LoadAsync方法
        await LoadAsync(input, config, ct);
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct)
    {
        if (_writer != null)
        {
            return _writer.DisposeAsync().AsTask();
        }
        return Task.CompletedTask;
    }
}
