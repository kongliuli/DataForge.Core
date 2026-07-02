using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.DataStandardizationTransformer;

public class DataStandardizationTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.DataStandardizationTransformer";
    public string Name => "数据标准化转换器";
    public string Description => "支持对数据进行标准化处理";
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
        var standardizationRules = ParseStandardizationRules(config);

        // 处理每条数据记录
        await foreach (var record in data)
        {
            var transformedRecord = new DataRecord();

            // 复制原始记录的所有字段
            foreach (var kvp in record)
            {
                transformedRecord[kvp.Key] = kvp.Value;
            }

            // 应用标准化规则
            foreach (var rule in standardizationRules)
            {
                if (transformedRecord.TryGetValue(rule.Field, out var value))
                {
                    try
                    {
                        var standardizedValue = ApplyStandardization(value, rule);
                        transformedRecord[rule.Field] = standardizedValue;
                    }
                    catch (Exception ex)
                    {
                        // 如果标准化失败，可以选择跳过该字段或使用默认值
                        // 可以在这里添加日志记录
                    }
                }
            }

            yield return transformedRecord;
        }
    }

    private List<StandardizationRule> ParseStandardizationRules(TransformConfig config)
    {
        var rules = new List<StandardizationRule>();

        // 解析标准化规则配置
        // 配置格式示例：
        // "Rules": "Field1:string:trim,Field2:datetime:yyyy-MM-dd,Field3:number:2,Field4:string:lower"
        if (config.TryGetValue("Rules", out var rulesValue))
        {
            var ruleStrings = rulesValue.Split(',');
            foreach (var ruleString in ruleStrings)
            {
                var parts = ruleString.Split(':');
                if (parts.Length >= 2)
                {
                    var field = parts[0].Trim();
                    var type = parts[1].Trim();
                    string format = null;

                    if (parts.Length >= 3)
                    {
                        format = parts[2].Trim();
                    }

                    rules.Add(new StandardizationRule
                    {
                        Field = field,
                        Type = type,
                        Format = format
                    });
                }
            }
        }

        return rules;
    }

    private object ApplyStandardization(object value, StandardizationRule rule)
    {
        if (value == null)
        {
            return null;
        }

        switch (rule.Type.ToLower())
        {
            case "string":
                return StandardizeString(value.ToString(), rule.Format);
            case "datetime":
                return StandardizeDateTime(value, rule.Format);
            case "number":
                return StandardizeNumber(value, rule.Format);
            case "enum":
                return StandardizeEnum(value, rule.Format);
            default:
                return value;
        }
    }

    private object StandardizeString(string value, string format)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        switch (format?.ToLower())
        {
            case "trim":
                return value.Trim();
            case "lower":
                return value.ToLower();
            case "upper":
                return value.ToUpper();
            case "title":
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
            case "remove-spaces":
                return Regex.Replace(value, @"\s+", "");
            case "remove-special":
                return Regex.Replace(value, @"[^a-zA-Z0-9]", "");
            default:
                return value;
        }
    }

    private object StandardizeDateTime(object value, string format)
    {
        if (value == null)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            if (!string.IsNullOrEmpty(format))
            {
                return dateTime.ToString(format);
            }
            else
            {
                return dateTime;
            }
        }
        else if (value is string dateString)
        {
            if (DateTime.TryParse(dateString, out var parsedDateTime))
            {
                if (!string.IsNullOrEmpty(format))
                {
                    return parsedDateTime.ToString(format);
                }
                else
                {
                    return parsedDateTime;
                }
            }
        }

        return value;
    }

    private object StandardizeNumber(object value, string format)
    {
        if (value == null)
        {
            return null;
        }

        if (double.TryParse(value.ToString(), out var number))
        {
            if (!string.IsNullOrEmpty(format) && int.TryParse(format, out var decimalPlaces))
            {
                return Math.Round(number, decimalPlaces);
            }
            else
            {
                return number;
            }
        }

        return value;
    }

    private object StandardizeEnum(object value, string format)
    {
        if (value == null)
        {
            return null;
        }

        // 简单的枚举标准化，将值转换为小写或大写
        var stringValue = value.ToString();
        switch (format?.ToLower())
        {
            case "lower":
                return stringValue.ToLower();
            case "upper":
                return stringValue.ToUpper();
            default:
                return stringValue;
        }
    }

    private class StandardizationRule
    {
        public string Field { get; set; }
        public string Type { get; set; }
        public string Format { get; set; }
    }
}
