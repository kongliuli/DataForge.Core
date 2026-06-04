using System;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Transforms;

/// <summary>
/// 数据映射转换器
/// </summary>
public class MapTransform<TInput, TOutput> : IDataTransform<TInput, TOutput>
{
    private readonly Func<TInput, TOutput> _mapper;

    public MapTransform(Func<TInput, TOutput> mapper)
    {
        _mapper = mapper;
    }

    public TOutput Transform(TInput input) => _mapper(input);
}

/// <summary>
/// 异步数据映射转换器
/// </summary>
public class AsyncMapTransform<TInput, TOutput> : IAsyncDataTransform<TInput, TOutput>
{
    private readonly Func<TInput, Task<TOutput>> _mapper;

    public AsyncMapTransform(Func<TInput, Task<TOutput>> mapper)
    {
        _mapper = mapper;
    }

    public Task<TOutput> TransformAsync(TInput input) => _mapper(input);
}

/// <summary>
/// 类型转换转换器
/// </summary>
public class CastTransform<TInput, TOutput> : IDataTransform<TInput, TOutput> where TOutput : TInput
{
    public TOutput Transform(TInput input) => (TOutput)input;
}

/// <summary>
/// 空值处理转换器
/// </summary>
public class DefaultIfEmptyTransform<T> : IDataTransform<T, T>
{
    private readonly T _defaultValue;

    public DefaultIfEmptyTransform(T defaultValue)
    {
        _defaultValue = defaultValue;
    }

    public T Transform(T input) => input ?? _defaultValue;
}

/// <summary>
/// 空值跳过转换器
/// </summary>
public class SkipNullsTransform<T> : IDataTransform<T?, T> where T : class
{
    public T Transform(T? input) => input ?? throw new ArgumentNullException(nameof(input));
}

/// <summary>
/// 日期格式化转换器
/// </summary>
public class DateFormatTransform : IDataTransform<DateTime, string>
{
    private readonly string _format;

    public DateFormatTransform(string format = "yyyy-MM-dd")
    {
        _format = format;
    }

    public string Transform(DateTime input) => input.ToString(_format);
}

/// <summary>
/// 字符串修剪转换器
/// </summary>
public class TrimTransform : IDataTransform<string, string>
{
    private readonly bool _removeEmptyEntries;

    public TrimTransform(bool removeEmptyEntries = false)
    {
        _removeEmptyEntries = removeEmptyEntries;
    }

    public string Transform(string input)
    {
        if (_removeEmptyEntries)
        {
            return input?.Trim() ?? string.Empty;
        }
        return input?.Trim() ?? string.Empty;
    }
}

/// <summary>
/// 大小写转换转换器
/// </summary>
public class ToCaseTransform : IDataTransform<string, string>
{
    private readonly bool _toUpper;

    public ToCaseTransform(bool toUpper = true)
    {
        _toUpper = toUpper;
    }

    public string Transform(string input) => _toUpper ? input?.ToUpper() ?? string.Empty : input?.ToLower() ?? string.Empty;
}

/// <summary>
/// 数值格式化转换器
/// </summary>
public class NumberFormatTransform : IDataTransform<decimal, string>
{
    private readonly string _format;

    public NumberFormatTransform(string format = "N2")
    {
        _format = format;
    }

    public string Transform(decimal input) => input.ToString(_format);
}

/// <summary>
/// 布尔值格式化转换器
/// </summary>
public class BoolFormatTransform : IDataTransform<bool, string>
{
    private readonly string _trueValue;
    private readonly string _falseValue;

    public BoolFormatTransform(string trueValue = "是", string falseValue = "否")
    {
        _trueValue = trueValue;
        _falseValue = falseValue;
    }

    public string Transform(bool input) => input ? _trueValue : _falseValue;
}

/// <summary>
/// 计算字段转换器（用于添加计算属性）
/// </summary>
public class ComputeTransform<TInput, TOutput> : IDataTransform<TInput, TOutput>
{
    private readonly Func<TInput, TOutput> _computer;

    public ComputeTransform(Func<TInput, TOutput> computer)
    {
        _computer = computer;
    }

    public TOutput Transform(TInput input) => _computer(input);
}

/// <summary>
/// 条件转换器
/// </summary>
public class ConditionalTransform<TInput, TOutput> : IDataTransform<TInput, TOutput?>
{
    private readonly Func<TInput, bool> _condition;
    private readonly Func<TInput, TOutput> _trueMapper;
    private readonly Func<TInput, TOutput> _falseMapper;

    public ConditionalTransform(
        Func<TInput, bool> condition,
        Func<TInput, TOutput> trueMapper,
        Func<TInput, TOutput> falseMapper)
    {
        _condition = condition;
        _trueMapper = trueMapper;
        _falseMapper = falseMapper;
    }

    public TOutput? Transform(TInput input) => _condition(input) ? _trueMapper(input) : _falseMapper(input);
}

/// <summary>
/// 查找转换器（类似于字典查找）
/// </summary>
public class LookupTransform<TKey, TValue> : IDataTransform<TKey, TValue?>
{
    private readonly Dictionary<TKey, TValue> _lookupTable;

    public LookupTransform(Dictionary<TKey, TValue> lookupTable)
    {
        _lookupTable = lookupTable;
    }

    public TValue? Transform(TKey input) => _lookupTable.TryGetValue(input, out var result) ? result : default;
}

/// <summary>
/// 组合转换器
/// </summary>
public class ComposeTransform<TInput, TIntermediate, TOutput> : IDataTransform<TInput, TOutput>
{
    private readonly IDataTransform<TInput, TIntermediate> _first;
    private readonly IDataTransform<TIntermediate, TOutput> _second;

    public ComposeTransform(
        IDataTransform<TInput, TIntermediate> first,
        IDataTransform<TIntermediate, TOutput> second)
    {
        _first = first;
        _second = second;
    }

    public TOutput Transform(TInput input) => _second.Transform(_first.Transform(input));
}
