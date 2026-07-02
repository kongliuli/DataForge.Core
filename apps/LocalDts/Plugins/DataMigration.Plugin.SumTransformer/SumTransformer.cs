namespace DataMigration.Plugin.SumTransformer;

using DataMigration.Contracts;
using System.Runtime.CompilerServices;

public class SumTransformer : ITransformer
{
    public string Id => "SumTransformer";
    public string Name => "Sum Aggregation Transformer";
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

    public async IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        // 从配置中读取需要求和的字段列表和输出字段名
        var fieldsToSum = config.GetValueOrDefault("FieldsToSum", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToList();
        var outputField = config.GetValueOrDefault("OutputField", "SumResult");

        double sum = 0;

        // 遍历所有记录，对指定字段求和
        await foreach (var record in input.WithCancellation(ct))
        {
            foreach (var field in fieldsToSum)
            {
                if (record.TryGetValue(field, out var value))
                {
                    if (value != null)
                    {
                        // 尝试将值转换为数值
                        if (double.TryParse(value.ToString(), out var numericValue))
                        {
                            sum += numericValue;
                        }
                    }
                }
            }

            // 将当前记录传递下去
            yield return record;
        }

        // 创建一个包含求和结果的新记录
        var resultRecord = new DataRecord();
        resultRecord.SetValue(outputField, sum);
        yield return resultRecord;
    }
}
