using DataMigration.Contracts;
using System.Runtime.CompilerServices;

namespace DataMigration.Plugin.EnumMappingTransformer;

public class EnumMappingTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.EnumMappingTransformer";
    public string Name => "枚举映射清洗算子";
    public Version Version => new(1, 0, 0);

    private string _fieldName = string.Empty;
    private Dictionary<string, string> _mapping = new();
    private string _unmappedStrategy = "keep";
    private string? _defaultValue;

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
        // 解析配置
        ParseConfig(config);

        await foreach (var record in input.WithCancellation(ct))
        {
            if (record.ContainsKey(_fieldName))
            {
                var enumValue = record[_fieldName]?.ToString();
                if (!string.IsNullOrEmpty(enumValue))
                {
                    if (_mapping.TryGetValue(enumValue, out var mappedValue))
                    {
                        record[_fieldName] = mappedValue;
                    }
                    else
                    {
                        // 处理未映射的枚举值
                        HandleUnmappedValue(record);
                    }
                }
            }
            yield return record;
        }
    }

    private void ParseConfig(TransformConfig config)
    {
        // 获取字段名
        if (config.TryGetValue("FieldName", out var fieldName))
        {
            _fieldName = fieldName;
        }

        // 获取映射规则
        for (int i = 0; ; i++)
        {
            if (config.TryGetValue($"Mapping.{i}.From", out var from) && 
                config.TryGetValue($"Mapping.{i}.To", out var to))
            {
                _mapping[from] = to;
            }
            else
            {
                break;
            }
        }

        // 获取未映射处理策略
        if (config.TryGetValue("UnmappedStrategy", out var strategy))
        {
            _unmappedStrategy = strategy;
        }

        // 获取默认值
        config.TryGetValue("DefaultValue", out _defaultValue);
    }

    private void HandleUnmappedValue(DataRecord record)
    {
        switch (_unmappedStrategy.ToLower())
        {
            case "default":
                // 使用默认值
                record[_fieldName] = _defaultValue;
                break;
            case "throw":
                // 抛出异常
                throw new InvalidOperationException($"未映射的枚举值: {record[_fieldName]} for field {_fieldName}");
            case "keep":
            default:
                // 保持原值
                break;
        }
    }
}