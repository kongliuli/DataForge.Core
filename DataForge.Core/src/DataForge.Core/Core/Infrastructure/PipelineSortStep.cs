namespace DataForge.Core.Core.Infrastructure;

internal interface IPipelineSortStep<T>
{
    IOrderedEnumerable<T> ApplyFirst(IEnumerable<T> items);
    IOrderedEnumerable<T> ApplyThen(IOrderedEnumerable<T> items);
}

internal sealed class PipelineSortStep<T, TKey> : IPipelineSortStep<T>
{
    private readonly Func<T, TKey> _keySelector;
    private readonly bool _ascending;

    public PipelineSortStep(Func<T, TKey> keySelector, bool ascending)
    {
        _keySelector = keySelector;
        _ascending = ascending;
    }

    public IOrderedEnumerable<T> ApplyFirst(IEnumerable<T> items) =>
        _ascending ? items.OrderBy(_keySelector) : items.OrderByDescending(_keySelector);

    public IOrderedEnumerable<T> ApplyThen(IOrderedEnumerable<T> items) =>
        _ascending ? items.ThenBy(_keySelector) : items.ThenByDescending(_keySelector);
}
