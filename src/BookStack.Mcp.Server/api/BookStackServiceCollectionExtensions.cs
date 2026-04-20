using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Api;

public static class BookStackServiceCollectionExtensions
{
    public static IServiceCollection AddBookStackApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BookStackApiClientOptions>(
            configuration.GetSection("BookStack"));

        services.AddSingleton<IValidateOptions<BookStackApiClientOptions>,
            BookStackApiClientOptionsValidator>();

        services.AddTransient<AuthenticationHandler>();
        services.AddTransient<RateLimitHandler>();

        services.AddHttpClient<IBookStackApiClient, BookStackApiClient>()
            .AddHttpMessageHandler<AuthenticationHandler>()
            .AddHttpMessageHandler<RateLimitHandler>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                });

        return services;
    }
}
