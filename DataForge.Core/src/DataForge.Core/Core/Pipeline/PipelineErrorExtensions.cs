namespace DataForge.Core.Core.Pipeline;

public static class PipelineErrorExtensions
{
    public static IDataPipeline<T> WithBadRowOutput<T>(this IDataPipeline<T> pipeline, string filePath)
    {
        if (pipeline is not DataPipeline<T> dp)
        {
            throw new NotSupportedException("WithBadRowOutput requires a DataForge pipeline instance.");
        }

        return dp.WithBadRowOutput(filePath);
    }
}
