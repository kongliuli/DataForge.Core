using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.AggregationTransformer;

public class AggregationTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.AggregationTransformer";
    public string Name => "聚合清洗算子";
    public Version Version => new(1, 0, 0);

    private const string AggregationTypeKey = "AggregationType";
    private const string GroupByKey = "GroupBy";
    private const string ValueFieldKey = "ValueField";
    private const string ResultFieldKey = "ResultField";

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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct
    )
    {
        // 读取配置
        var aggregationType = config.GetValueOrDefault(AggregationTypeKey, "Average");
        var groupByFields = config.GetValueOrDefault(GroupByKey, "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        var valueField = config.GetValueOrDefault(ValueFieldKey, "Value");
        var resultField = config.GetValueOrDefault(ResultFieldKey, $"{aggregationType}Result");

        // 收集所有数据记录
        var records = await input.ToListAsync(ct);

        // 按分组字段分组
        if (groupByFields.Count > 0)
        {
            var groupedRecords = records.GroupBy(r => GetGroupKey(r, groupByFields));
            foreach (var group in groupedRecords)
            {
                var aggregatedValue = ApplyAggregation(group.ToList(), valueField, aggregationType);
                var resultRecord = new DataRecord();

                // 保留分组字段的值
                var groupKeyParts = group.Key.Split('|');
                for (int i = 0; i < groupByFields.Count && i < groupKeyParts.Length; i++)
                {
                    resultRecord[groupByFields[i]] = groupKeyParts[i];
                }

                // 设置聚合结果
                resultRecord[resultField] = aggregatedValue;
                yield return resultRecord;
            }
        }
        else
        {
            // 不分组，对所有记录应用聚合
            var aggregatedValue = ApplyAggregation(records, valueField, aggregationType);
            var resultRecord = new DataRecord();
            resultRecord[resultField] = aggregatedValue;
            yield return resultRecord;
        }
    }

    private string GetGroupKey(DataRecord record, List<string> groupByFields)
    {
        return string.Join("|", groupByFields.Select(field => record.TryGetValue(field, out var value) ? value?.ToString() ?? "" : ""));
    }

    private object? ApplyAggregation(List<DataRecord> records, string valueField, string aggregationType)
    {
        // 提取值字段的数据
        var values = records
            .Select(r => r.TryGetValue(valueField, out var value) ? value : null)
            .Where(v => v != null)
            .ToList();

        if (values.Count == 0)
        {
            return null;
        }

        // 尝试将值转换为数值类型
        var numericValues = new List<double>();
        foreach (var value in values)
        {
            if (value is int intValue)
            {
                numericValues.Add(intValue);
            }
            else if (value is long longValue)
            {
                numericValues.Add(longValue);
            }
            else if (value is float floatValue)
            {
                numericValues.Add(floatValue);
            }
            else if (value is double doubleValue)
            {
                numericValues.Add(doubleValue);
            }
            else if (value is decimal decimalValue)
            {
                numericValues.Add(Convert.ToDouble(decimalValue));
            }
            else if (double.TryParse(value?.ToString(), out var parsedValue))
            {
                numericValues.Add(parsedValue);
            }
        }

        if (numericValues.Count == 0)
        {
            // 如果没有数值类型，尝试处理其他类型
            return ApplyNonNumericAggregation(values, aggregationType);
        }

        // 应用数值聚合
        return ApplyNumericAggregation(numericValues, aggregationType);
    }

    private object ApplyNumericAggregation(List<double> values, string aggregationType)
    {
        return aggregationType switch
        {
            "Average" => values.Average(),
            "Sum" => values.Sum(),
            "Max" => values.Max(),
            "Min" => values.Min(),
            "Count" => values.Count,
            "Median" => CalculateMedian(values),
            "StandardDeviation" => CalculateStandardDeviation(values),
            _ => values.Average() // 默认使用平均值
        };
    }

    private object? ApplyNonNumericAggregation(List<object?> values, string aggregationType)
    {
        return aggregationType switch
        {
            "Count" => values.Count,
            "First" => values.FirstOrDefault(),
            "Last" => values.LastOrDefault(),
            "DistinctCount" => values.Distinct().Count(),
            _ => null // 对于其他聚合类型，返回 null
        };
    }

    private double CalculateMedian(List<double> values)
    {
        var sortedValues = values.OrderBy(v => v).ToList();
        int count = sortedValues.Count;
        if (count % 2 == 0)
        {
            return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
        }
        else
        {
            return sortedValues[count / 2];
        }
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        double average = values.Average();
        double sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}
