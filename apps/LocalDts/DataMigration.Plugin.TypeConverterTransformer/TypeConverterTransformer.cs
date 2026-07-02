using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.TypeConverterTransformer;

public class TypeConverterTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.TypeConverterTransformer";
    public string Name => "数据类型转换转换器";
    public string Description => "支持在不同数据类型之间进行转换";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 ITransformer 中不需要实现，因为转换是通过 TransformAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭逻辑，这里可以为空
    }

    public async IAsyncEnumerable<DataRecord> TransformAsync(IAsyncEnumerable<DataRecord> data, TransformConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var conversionRules = ParseConversionRules(config);

        // 处理每条数据记录
        await foreach (var record in data)
        {
            var transformedRecord = new DataRecord();

            // 复制原始记录的所有字段
            foreach (var kvp in record)
            {
                transformedRecord[kvp.Key] = kvp.Value;
            }

            // 应用转换规则
            foreach (var rule in conversionRules)
            {
                if (transformedRecord.TryGetValue(rule.SourceField, out var value))
                {
                    try
                    {
                        var convertedValue = ConvertValue(value, rule.TargetType, rule.Format);
                        transformedRecord[rule.TargetField ?? rule.SourceField] = convertedValue;
                    }
                    catch (Exception ex)
                    {
                        // 如果转换失败，可以选择跳过该字段或使用默认值
                        if (rule.DefaultValue != null)
                        {
                            transformedRecord[rule.TargetField ?? rule.SourceField] = rule.DefaultValue;
                        }
                        // 可以在这里添加日志记录
                    }
                }
            }

            yield return transformedRecord;
        }
    }

    private List<ConversionRule> ParseConversionRules(TransformConfig config)
    {
        var rules = new List<ConversionRule>();

        // 解析转换规则配置
        // 配置格式示例：
        // "Rules": "Field1:int,Field2:bool,Field3:DateTime:yyyy-MM-dd"
        if (config.TryGetValue("Rules", out var rulesValue))
        {
            var ruleStrings = rulesValue.Split(',');
            foreach (var ruleString in ruleStrings)
            {
                var parts = ruleString.Split(':');
                if (parts.Length >= 2)
                {
                    var sourceField = parts[0].Trim();
                    var targetType = parts[1].Trim();
                    string format = null;
                    string targetField = null;
                    object defaultValue = null;

                    // 检查是否指定了目标字段
                    if (sourceField.Contains('>'))
                    {
                        var fieldParts = sourceField.Split('>');
                        sourceField = fieldParts[0].Trim();
                        targetField = fieldParts[1].Trim();
                    }

                    // 检查是否指定了格式
                    if (parts.Length >= 3)
                    {
                        format = parts[2].Trim();
                    }

                    // 检查是否指定了默认值
                    if (parts.Length >= 4)
                    {
                        defaultValue = parts[3].Trim();
                    }

                    rules.Add(new ConversionRule
                    {
                        SourceField = sourceField,
                        TargetField = targetField,
                        TargetType = targetType,
                        Format = format,
                        DefaultValue = defaultValue
                    });
                }
            }
        }

        return rules;
    }

    private object ConvertValue(object value, string targetType, string format)
    {
        if (value == null)
        {
            return null;
        }

        targetType = targetType.ToLower();

        switch (targetType)
        {
            case "int":
            case "integer":
                return Convert.ToInt32(value);
            case "long":
                return Convert.ToInt64(value);
            case "double":
                return Convert.ToDouble(value);
            case "decimal":
                return Convert.ToDecimal(value);
            case "float":
                return Convert.ToSingle(value);
            case "bool":
            case "boolean":
                return Convert.ToBoolean(value);
            case "datetime":
                if (!string.IsNullOrEmpty(format))
                {
                    return DateTime.ParseExact(value.ToString(), format, null);
                }
                else
                {
                    return Convert.ToDateTime(value);
                }
            case "string":
                if (!string.IsNullOrEmpty(format) && value is DateTime dateTime)
                {
                    return dateTime.ToString(format);
                }
                else
                {
                    return value.ToString();
                }
            case "guid":
                return Guid.Parse(value.ToString());
            default:
                // 未知类型，返回原始值
                return value;
        }
    }

    private class ConversionRule
    {
        public string SourceField { get; set; }
        public string TargetField { get; set; }
        public string TargetType { get; set; }
        public string Format { get; set; }
        public object DefaultValue { get; set; }
    }
}
