#pragma warning disable CA2007

using Testcontainers.PostgreSql;

using TUnit.Core.Interfaces;

namespace BookStack.Mcp.Server.Tests.Migrations.Infrastructure;

public sealed class PostgresMigrationContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("bookstack_vectors")
        .WithUsername("bookstack")
        .WithPassword("bookstack")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Skip.Test($"Docker unavailable for Postgres migration test: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
