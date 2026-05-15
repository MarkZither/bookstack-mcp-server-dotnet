using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarkZither.Rag.Chunking;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChunking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITokenEncoder, TiktokenEncoder>();
        services.TryAddSingleton<IChunkingService, SlideWindowChunkingService>();

        return services;
    }
}
