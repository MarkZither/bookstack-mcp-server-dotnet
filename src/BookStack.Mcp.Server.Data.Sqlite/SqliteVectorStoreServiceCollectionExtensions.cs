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
            // Ensure the sync_metadata table exists before first use.
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();
            return new SqliteVectorStore(connectionString, factory);
        });

        return services;
    }
}
