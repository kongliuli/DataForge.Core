using Microsoft.Extensions.DependencyInjection;

namespace DataForge.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataForge(this IServiceCollection services)
    {
        services.AddSingleton<DataForgeEntry>();
        return services;
    }
}

public sealed class DataForgeEntry
{
    public DataForgePipeline Pipeline { get; } = new();
}
