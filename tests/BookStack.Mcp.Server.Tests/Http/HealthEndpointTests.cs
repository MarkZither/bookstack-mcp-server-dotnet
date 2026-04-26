using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Http;

[ClassDataSource<McpHttpTestFactory>(Shared = SharedType.PerClass)]
public sealed class HealthEndpointTests(McpHttpTestFactory factory)
{
    [Test]
    public async Task HealthEndpoint_Returns200_WithStatusOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }
}

[ClassDataSource<McpHttpTestFactoryWithAuth>(Shared = SharedType.PerClass)]
public sealed class HealthEndpointWithAuthTests(McpHttpTestFactoryWithAuth factory)
{
    [Test]
    public async Task HealthEndpoint_Bypasses_Auth()
    {
        var client = factory.CreateClient();

        // No Authorization header — health must be reachable even when auth is configured
        var response = await client.GetAsync("/health").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
