namespace DataForge.Core.Core.Transforms;

public interface IDataTransform<in TSource, out TResult>
{
    TResult Transform(TSource source);
}

public interface IAsyncDataTransform<in TSource, TResult>
{
    Task<TResult> TransformAsync(TSource source);
}