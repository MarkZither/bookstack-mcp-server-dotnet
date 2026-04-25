using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BookStack.Mcp.Server.Tests.Http;

public class McpHttpTestFactory : WebApplicationFactory<Program>
{
    static McpHttpTestFactory()
    {
        Environment.SetEnvironmentVariable("BOOKSTACK_MCP_TRANSPORT", "http");
        Environment.SetEnvironmentVariable("BOOKSTACK_BASE_URL", "http://fake.bookstack.test");
        Environment.SetEnvironmentVariable("BOOKSTACK_TOKEN_SECRET", "fake-id:fake-secret");
        Environment.SetEnvironmentVariable("BOOKSTACK_MCP_HTTP_AUTH_TOKEN", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOKSTACK_MCP_HTTP_AUTH_TOKEN"] = string.Empty
            }));
    }
}

public sealed class McpHttpTestFactoryWithAuth : McpHttpTestFactory
{
    internal const string Token = "test-secret-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOKSTACK_MCP_HTTP_AUTH_TOKEN"] = Token
            }));
    }
}

