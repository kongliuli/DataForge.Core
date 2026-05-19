namespace DataForge.Core.Core.Transforms;

internal class WhereTransform<T> : IDataTransform<T, T>
{
    private readonly Func<T, bool> _predicate;

    public WhereTransform(Func<T, bool> predicate)
    {
        _predicate = predicate;
    }

    public T Transform(T source)
    {
        return _predicate(source) ? source : default!;
    }
}

internal class AsyncWhereTransform<T> : IAsyncDataTransform<T, T>
{
    private readonly Func<T, Task<bool>> _predicate;

    public AsyncWhereTransform(Func<T, Task<bool>> predicate)
    {
        _predicate = predicate;
    }

    public async Task<T> TransformAsync(T source)
    {
        return await _predicate(source) ? source : default!;
    }
}