using DataMigration.Contracts;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.RenameTransformer;

public class RenameTransformer : ITransformer
{
    public string Id => "RenameTransformer";
    public string Name => "Rename Transformer";
    public string Description => "重命名字段名的转换器";
    public Version Version => new(1, 0, 0);

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

    public IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        CancellationToken ct
    )
    {
        return TransformInternalAsync(input, config, ct);
    }

    private async IAsyncEnumerable<DataRecord> TransformInternalAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        // 解析重命名规则
        var renameRules = new Dictionary<string, string>();
        foreach (var kvp in config)
        {
            if (kvp.Key.StartsWith("rename_"))
            {
                var oldField = kvp.Key.Substring(7); // 移除 "rename_" 前缀
                var newField = kvp.Value;
                renameRules[oldField] = newField;
            }
        }

        await foreach (var record in input.WithCancellation(ct))
        {
            var transformedRecord = new DataRecord();
            
            // 复制所有原始字段
            foreach (var kvp in record)
            {
                transformedRecord[kvp.Key] = kvp.Value;
            }

            // 应用重命名规则
            foreach (var rule in renameRules)
            {
                if (transformedRecord.ContainsKey(rule.Key))
                {
                    var value = transformedRecord[rule.Key];
                    transformedRecord.Remove(rule.Key);
                    transformedRecord[rule.Value] = value;
                }
                // 如果字段不存在，跳过处理
            }

            yield return transformedRecord;
        }
    }
}