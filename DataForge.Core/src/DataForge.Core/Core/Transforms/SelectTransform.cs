namespace DataForge.Core.Core.Transforms;

internal class SelectTransform<TSource, TResult> : IDataTransform<TSource, TResult>
{
    private readonly Func<TSource, TResult> _selector;

    public SelectTransform(Func<TSource, TResult> selector)
    {
        _selector = selector;
    }

    public TResult Transform(TSource source)
    {
        return _selector(source);
    }
}

internal class AsyncSelectTransform<TSource, TResult> : IAsyncDataTransform<TSource, TResult>
{
    private readonly Func<TSource, Task<TResult>> _selector;

    public AsyncSelectTransform(Func<TSource, Task<TResult>> selector)
    {
        _selector = selector;
    }

    public Task<TResult> TransformAsync(TSource source)
    {
        return _selector(source);
    }
}