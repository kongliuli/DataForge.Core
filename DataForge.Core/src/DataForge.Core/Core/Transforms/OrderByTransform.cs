namespace DataForge.Core.Core.Transforms;

internal class OrderByTransform<T, TKey> : IDataTransform<T, (T Item, TKey Key)>
{
    private readonly Func<T, TKey> _keySelector;
    private readonly bool _ascending;

    public OrderByTransform(Func<T, TKey> keySelector, bool ascending)
    {
        _keySelector = keySelector;
        _ascending = ascending;
    }

    public (T Item, TKey Key) Transform(T source)
    {
        return (source, _keySelector(source));
    }
}

internal class ThenByTransform<T, TKey> : IDataTransform<T, (T Item, TKey Key)>
{
    private readonly Func<T, TKey> _keySelector;
    private readonly bool _ascending;

    public ThenByTransform(Func<T, TKey> keySelector, bool ascending)
    {
        _keySelector = keySelector;
        _ascending = ascending;
    }

    public (T Item, TKey Key) Transform(T source)
    {
        return (source, _keySelector(source));
    }
}