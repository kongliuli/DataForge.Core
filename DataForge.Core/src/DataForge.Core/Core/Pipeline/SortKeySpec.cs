namespace DataForge.Core.Core.Pipeline;

internal sealed class SortKeySpec
{
    public SortKeySpec(Func<object, object?> keySelector, Comparison<object?> comparison, bool descending)
    {
        KeySelector = keySelector;
        Comparison = comparison;
        Descending = descending;
    }

    public Func<object, object?> KeySelector { get; }
    public Comparison<object?> Comparison { get; }
    public bool Descending { get; }

    public static SortKeySpec Create<T, TKey>(Func<T, TKey> selector, bool descending)
    {
        var comparer = Comparer<TKey>.Default;
        return new SortKeySpec(
            obj => selector((T)obj)!,
            (a, b) => comparer.Compare((TKey)a!, (TKey)b!),
            descending);
    }
}
