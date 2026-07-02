using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.DataFilterTransformer;

public class DataFilterTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.DataFilterTransformer";
    public string Name => "数据过滤转换器";
    public string Description => "根据条件过滤数据记录";
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
        var filterRules = ParseFilterRules(config);
        var filterMode = config.TryGetValue("FilterMode", out var modeValue) ? modeValue.ToLower() : "and";

        // 处理每条数据记录
        await foreach (var record in data)
        {
            // 检查是否满足所有过滤规则
            bool shouldInclude = true;

            if (filterMode == "and")
            {
                // 所有规则都必须满足
                foreach (var rule in filterRules)
                {
                    if (!EvaluateRule(record, rule))
                    {
                        shouldInclude = false;
                        break;
                    }
                }
            }
            else
            {
                // 至少满足一条规则
                shouldInclude = false;
                foreach (var rule in filterRules)
                {
                    if (EvaluateRule(record, rule))
                    {
                        shouldInclude = true;
                        break;
                    }
                }
            }

            // 如果满足条件，包含该记录
            if (shouldInclude)
            {
                yield return record;
            }
        }
    }

    private List<FilterRule> ParseFilterRules(TransformConfig config)
    {
        var rules = new List<FilterRule>();

        // 解析过滤规则配置
        // 配置格式示例：
        // "Rules": "Field1>10,Field2=test,Field3!=null"
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

    private FilterRule ParseRuleString(string ruleString)
    {
        // 支持的操作符：=, !=, >, <, >=, <=, contains, startsWith, endsWith
        var operators = new[] { ">=", "<=", "!=", "=", ">", "<", "contains", "startsWith", "endsWith" };
        int operatorIndex = -1;
        string op = null;

        foreach (var _op in operators)
        {
            operatorIndex = ruleString.IndexOf(_op);
            if (operatorIndex != -1)
            {
                op = _op;
                break;
            }
        }

        if (operatorIndex == -1)
        {
            return null;
        }

        var field = ruleString.Substring(0, operatorIndex).Trim();
        var value = ruleString.Substring(operatorIndex + op.Length).Trim();

        // 处理引号包围的值
        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            value = value.Substring(1, value.Length - 2);
        }

        return new FilterRule
        {
            Field = field,
            Operator = op,
            Value = value
        };
    }

    private bool EvaluateRule(DataRecord record, FilterRule rule)
    {
        if (!record.TryGetValue(rule.Field, out var value))
        {
            // 字段不存在，根据操作符判断
            switch (rule.Operator)
            {
                case "=":
                case "!=":
                    return rule.Operator == "!=";
                default:
                    return false;
            }
        }

        // 根据操作符进行比较
        switch (rule.Operator)
        {
            case "=":
                return CompareValues(value, rule.Value) == 0;
            case "!=":
                return CompareValues(value, rule.Value) != 0;
            case ">":
                return CompareValues(value, rule.Value) > 0;
            case "<":
                return CompareValues(value, rule.Value) < 0;
            case ">=":
                return CompareValues(value, rule.Value) >= 0;
            case "<=":
                return CompareValues(value, rule.Value) <= 0;
            case "contains":
                return value.ToString().Contains(rule.Value);
            case "startsWith":
                return value.ToString().StartsWith(rule.Value);
            case "endsWith":
                return value.ToString().EndsWith(rule.Value);
            default:
                return false;
        }
    }

    private int CompareValues(object value1, string value2)
    {
        // 尝试将值转换为相同类型进行比较
        if (value1 is IComparable comparable)
        {
            try
            {
                // 尝试将字符串值转换为与 value1 相同的类型
                var convertedValue2 = Convert.ChangeType(value2, value1.GetType());
                return comparable.CompareTo(convertedValue2);
            }
            catch
            {
                // 转换失败，使用字符串比较
                return value1.ToString().CompareTo(value2);
            }
        }

        // 如果 value1 不是可比较的，使用字符串比较
        return value1.ToString().CompareTo(value2);
    }

    private class FilterRule
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
    }
}
