using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.DataAggregationTransformer;

public class DataAggregationTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.DataAggregationTransformer";
    public string Name => "数据聚合转换器";
    public string Description => "支持对数据进行聚合操作";
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
        var groupByField = config.TryGetValue("GroupBy", out var groupByValue) ? groupByValue : null;
        var aggregationRules = ParseAggregationRules(config);

        // 收集所有数据记录
        var records = new List<DataRecord>();
        await foreach (var record in data)
        {
            records.Add(record);
        }

        // 执行聚合操作
        var aggregatedRecords = PerformAggregation(records, groupByField, aggregationRules);

        // 输出聚合结果
        foreach (var aggregatedRecord in aggregatedRecords)
        {
            yield return aggregatedRecord;
        }
    }

    private List<AggregationRule> ParseAggregationRules(TransformConfig config)
    {
        var rules = new List<AggregationRule>();

        // 解析聚合规则配置
        // 配置格式示例：
        // "Rules": "Sum(Amount),Count(*),Avg(Price),Max(Score),Min(Rating)"
        if (config.TryGetValue("Rules", out var rulesValue))
        {
            var ruleStrings = rulesValue.Split(',');
            foreach (var ruleString in ruleStrings)
            {
                var rule = ParseRuleString(ruleString);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }
        }

        return rules;
    }

    private AggregationRule ParseRuleString(string ruleString)
    {
        // 解析聚合规则，格式为：Function(Field)
        var openParenIndex = ruleString.IndexOf('(');
        var closeParenIndex = ruleString.LastIndexOf(')');

        if (openParenIndex == -1 || closeParenIndex == -1 || closeParenIndex < openParenIndex)
        {
            return null;
        }

        var function = ruleString.Substring(0, openParenIndex).Trim();
        var field = ruleString.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();

        return new AggregationRule
        {
            Function = function,
            Field = field
        };
    }

    private List<DataRecord> PerformAggregation(List<DataRecord> records, string groupByField, List<AggregationRule> aggregationRules)
    {
        var result = new List<DataRecord>();

        if (string.IsNullOrEmpty(groupByField))
        {
            // 全局聚合（不分组）
            var aggregatedRecord = new DataRecord();
            foreach (var rule in aggregationRules)
            {
                var value = CalculateAggregation(records, rule);
                aggregatedRecord[$"{rule.Function}({rule.Field})"] = value;
            }
            result.Add(aggregatedRecord);
        }
        else
        {
            // 按字段分组聚合
            var groups = records.GroupBy(r => r.TryGetValue(groupByField, out var value) ? value : null);
            foreach (var group in groups)
            {
                var aggregatedRecord = new DataRecord();
                aggregatedRecord[groupByField] = group.Key;
                
                foreach (var rule in aggregationRules)
                {
                    var value = CalculateAggregation(group.ToList(), rule);
                    aggregatedRecord[$"{rule.Function}({rule.Field})"] = value;
                }
                result.Add(aggregatedRecord);
            }
        }

        return result;
    }

    private object CalculateAggregation(List<DataRecord> records, AggregationRule rule)
    {
        // 过滤出包含指定字段的记录
        var validRecords = records.Where(r => r.TryGetValue(rule.Field, out var value) && value != null).ToList();

        if (validRecords.Count == 0)
        {
            return null;
        }

        switch (rule.Function.ToLower())
        {
            case "sum":
                return Sum(validRecords, rule.Field);
            case "count":
                if (rule.Field == "*")
                    return validRecords.Count;
                else
                    return validRecords.Count;
            case "avg":
            case "average":
                return Average(validRecords, rule.Field);
            case "max":
                return Max(validRecords, rule.Field);
            case "min":
                return Min(validRecords, rule.Field);
            case "first":
                return validRecords[0][rule.Field];
            case "last":
                return validRecords[validRecords.Count - 1][rule.Field];
            default:
                return null;
        }
    }

    private object Sum(List<DataRecord> records, string field)
    {
        // 尝试将值转换为数值类型并求和
        try
        {
            var sum = 0.0;
            foreach (var record in records)
            {
                if (record.TryGetValue(field, out var value))
                {
                    sum += Convert.ToDouble(value);
                }
            }
            return sum;
        }
        catch
        {
            return null;
        }
    }

    private object Average(List<DataRecord> records, string field)
    {
        // 尝试将值转换为数值类型并计算平均值
        try
        {
            var sum = 0.0;
            var count = 0;
            foreach (var record in records)
            {
                if (record.TryGetValue(field, out var value))
                {
                    sum += Convert.ToDouble(value);
                    count++;
                }
            }
            return count > 0 ? sum / count : null;
        }
        catch
        {
            return null;
        }
    }

    private object Max(List<DataRecord> records, string field)
    {
        // 尝试找出最大值
        try
        {
            var values = records
                .Where(r => r.TryGetValue(field, out var value))
                .Select(r => r[field])
                .ToList();
            
            if (values.Count == 0)
                return null;

            return values.Max(v => Convert.ToDouble(v));
        }
        catch
        {
            return null;
        }
    }

    private object Min(List<DataRecord> records, string field)
    {
        // 尝试找出最小值
        try
        {
            var values = records
                .Where(r => r.TryGetValue(field, out var value))
                .Select(r => r[field])
                .ToList();
            
            if (values.Count == 0)
                return null;

            return values.Min(v => Convert.ToDouble(v));
        }
        catch
        {
            return null;
        }
    }

    private class AggregationRule
    {
        public string Function { get; set; }
        public string Field { get; set; }
    }
}
