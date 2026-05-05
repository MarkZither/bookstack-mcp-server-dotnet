using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookStack.Mcp.Server.Tests.Admin;

/// <summary>
/// WebApplicationFactory for admin sidecar integration tests.
/// Uses http transport with a fixed admin port and an in-memory IVectorStore.
/// </summary>
public class AdminSidecarTestFactory : WebApplicationFactory<Program>
{
    internal const int AdminPort = 5175;

    static AdminSidecarTestFactory()
    {
        // Set env vars before any host is built so top-level Program.cs reads
        // (before WebApplication.CreateBuilder) pick up the correct values.
        Environment.SetEnvironmentVariable("BOOKSTACK_MCP_TRANSPORT", "http");
        Environment.SetEnvironmentVariable("BOOKSTACK_BASE_URL", "http://fake.bookstack.test");
        Environment.SetEnvironmentVariable("BOOKSTACK_TOKEN_SECRET", "fake-id:fake-secret");
        Environment.SetEnvironmentVariable("BOOKSTACK_ADMIN_PORT", AdminPort.ToString());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Provide config-layer values so app.Configuration (post-Build) resolves
                // correctly even if the env var changes between host builds.
                ["BOOKSTACK_ADMIN_PORT"] = AdminPort.ToString(),
                ["BOOKSTACK_MCP_HTTP_AUTH_TOKEN"] = string.Empty,
            }));

        builder.ConfigureServices(services =>
        {
            // Replace IVectorStore with the in-memory fake so admin/status works
            // without a real database.
            var existing = services
                .Where(d => d.ServiceType == typeof(IVectorStore))
                .ToList();
            foreach (var d in existing)
            {
                services.Remove(d);
            }

            services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        });
    }

    /// <summary>
    /// Creates an HttpClient pre-configured to send requests with the admin
    /// Host header so that RequireHost constraints are satisfied.
    /// </summary>
    internal HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Host = $"127.0.0.1:{AdminPort}";
        return client;
    }
}

/// <summary>
/// Factory variant where BOOKSTACK_ADMIN_PORT=0 (sidecar disabled).
/// Overrides only the config-layer key so routes are not mapped;
/// DI still has IAdminTaskQueue registered (from env var = 5175) which is harmless.
/// </summary>
public sealed class AdminDisabledTestFactory : AdminSidecarTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // Override at the config layer — Program.cs reads effectiveAdminPort from
        // app.Configuration (post-Build) so routes won't be registered.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOKSTACK_ADMIN_PORT"] = "0",
            }));
    }
}
