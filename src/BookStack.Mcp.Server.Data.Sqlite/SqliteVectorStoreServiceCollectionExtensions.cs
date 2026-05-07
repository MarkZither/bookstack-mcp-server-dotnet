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
            // EnsureCreated only acts when the DB file does not exist.
            // If a pre-existing DB was created by SqliteCollection (which only
            // creates page_vectors), sync_metadata will be absent. Run an
            // explicit CREATE TABLE IF NOT EXISTS so the table always exists
            // regardless of how the file was first created.
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();
            ctx.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "sync_metadata" (
                    "Key"   TEXT NOT NULL CONSTRAINT "PK_sync_metadata" PRIMARY KEY,
                    "Value" TEXT NOT NULL
                )
                """);
            return new SqliteVectorStore(connectionString, factory);
        });

        return services;
    }
}
