using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStack.Mcp.Server.Data.Sqlite;

public static class SqliteVectorStoreServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteVectorStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<SyncMetadataDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IVectorStore>(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<SyncMetadataDbContext>>();
            return new SqliteVectorStore(connectionString, factory);
        });

        return services;
    }
}
