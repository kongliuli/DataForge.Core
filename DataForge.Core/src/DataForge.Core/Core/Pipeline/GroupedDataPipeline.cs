using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

internal class GroupedDataPipeline<TKey, TElement> : IGroupedDataPipeline<TKey, TElement> where TKey : notnull
{
    private readonly Func<CancellationToken, IAsyncEnumerable<TElement>> _sourceFactory;
    private readonly Func<TElement, TKey> _keySelector;

    public GroupedDataPipeline(
        Func<CancellationToken, IAsyncEnumerable<TElement>> sourceFactory,
        Func<TElement, TKey> keySelector)
    {
        _sourceFactory = sourceFactory;
        _keySelector = keySelector;
    }

    public IDataPipeline<TResult> Select<TResult>(Func<IGrouping<TKey, TElement>, TResult> selector)
    {
        return new DataPipeline<TResult>((ct) =>
            SelectGroupsInternal(_sourceFactory(ct), _keySelector, selector, ct));
    }

    public IDataPipeline<IGrouping<TKey, TElement>> OrderBy<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector)
    {
        return new DataPipeline<IGrouping<TKey, TElement>>((ct) =>
            OrderByGroupsInternal(_sourceFactory(ct), _keySelector, keySelector, ascending: true, ct));
    }

    public IDataPipeline<IGrouping<TKey, TElement>> OrderByDescending<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector)
    {
        return new DataPipeline<IGrouping<TKey, TElement>>((ct) =>
            OrderByGroupsInternal(_sourceFactory(ct), _keySelector, keySelector, ascending: false, ct));
    }

    public async Task<List<IGrouping<TKey, TElement>>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var groups = await GetGroupsAsync(cancellationToken).ConfigureAwait(false);
        return groups.Cast<IGrouping<TKey, TElement>>().ToList();
    }

    public async Task<Dictionary<TKey, List<TElement>>> ToDictionaryAsync(CancellationToken cancellationToken = default)
    {
        var groups = await GetGroupsAsync(cancellationToken).ConfigureAwait(false);
        return groups.ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<List<IGrouping<TKey, TElement>>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        var items = new List<TElement>();
        await foreach (var item in _sourceFactory(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }
        return items.GroupBy(_keySelector).Cast<IGrouping<TKey, TElement>>().ToList();
    }

    private static async IAsyncEnumerable<TResult> SelectGroupsInternal<TResult>(
        IAsyncEnumerable<TElement> source,
        Func<TElement, TKey> keySelector,
        Func<IGrouping<TKey, TElement>, TResult> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var items = new List<TElement>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            items.Add(item);
        }
        foreach (var group in items.GroupBy(keySelector))
        {
            yield return selector(group);
        }
    }

    private static async IAsyncEnumerable<IGrouping<TKey, TElement>> OrderByGroupsInternal<TKey2>(
        IAsyncEnumerable<TElement> source,
        Func<TElement, TKey> keySelector,
        Func<IGrouping<TKey, TElement>, TKey2> orderKeySelector,
        bool ascending,
        CancellationToken ct)
    {
        var items = new List<TElement>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            items.Add(item);
        }
        var groups = ascending
            ? items.GroupBy(keySelector).OrderBy(orderKeySelector)
            : items.GroupBy(keySelector).OrderByDescending(orderKeySelector);

        foreach (var group in groups)
        {
            yield return group;
        }
    }
}