using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BookStack.Mcp.Server.Data.Postgres;

public static class PostgresVectorStoreServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresVectorStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<VectorDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.UseVector()));

        services.AddSingleton<IVectorStore, PgVectorStore>();

        return services;
    }
}
