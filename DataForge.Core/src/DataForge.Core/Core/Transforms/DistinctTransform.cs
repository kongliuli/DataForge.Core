using System.Collections.Generic;

namespace DataForge.Core.Core.Transforms;

internal class DistinctTransform<T> : IDataTransform<T, T>
{
    private readonly HashSet<T> _seen = new();

    public T Transform(T source)
    {
        if (_seen.Add(source))
        {
            return source;
        }
        return default!;
    }
}

internal class DistinctByTransform<T, TKey> : IDataTransform<T, T>
{
    private readonly HashSet<TKey> _seen = new();
    private readonly Func<T, TKey> _keySelector;

    public DistinctByTransform(Func<T, TKey> keySelector)
    {
        _keySelector = keySelector;
    }

    public T Transform(T source)
    {
        var key = _keySelector(source);
        if (_seen.Add(key))
        {
            return source;
        }
        return default!;
    }
}