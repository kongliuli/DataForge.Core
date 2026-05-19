namespace DataForge.Core.Core.Transforms;

internal class SkipTransform<T> : IDataTransform<T, T>
{
    private readonly int _count;
    private int _skipped;

    public SkipTransform(int count)
    {
        _count = count;
    }

    public T Transform(T source)
    {
        if (_skipped < _count)
        {
            _skipped++;
            return default!;
        }
        return source;
    }
}

internal class TakeTransform<T> : IDataTransform<T, T>
{
    private readonly int _count;
    private int _taken;

    public TakeTransform(int count)
    {
        _count = count;
    }

    public T Transform(T source)
    {
        if (_taken < _count)
        {
            _taken++;
            return source;
        }
        return default!;
    }
}