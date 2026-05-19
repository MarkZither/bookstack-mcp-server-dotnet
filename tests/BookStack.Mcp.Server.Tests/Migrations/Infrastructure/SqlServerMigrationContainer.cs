#pragma warning disable CA2007

using Testcontainers.MsSql;

using TUnit.Core.Interfaces;

namespace BookStack.Mcp.Server.Tests.Migrations.Infrastructure;

public sealed class SqlServerMigrationContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Skip.Test($"Docker unavailable for SQL Server migration test: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
