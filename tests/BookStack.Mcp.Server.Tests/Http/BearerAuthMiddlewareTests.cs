using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace BookStack.Mcp.Server.Tests.Http;

[ClassDataSource<McpHttpTestFactoryWithAuth>(Shared = SharedType.PerClass)]
public sealed class BearerAuthMiddlewareWithTokenTests(McpHttpTestFactoryWithAuth factory) : IDisposable
{
    public static McpHttpTestFactoryWithAuth GetFactory() => new();

    public void Dispose() { }

    [Test]
    public async Task Mcp_WithValidToken_IsNotRejectedByAuth()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", McpHttpTestFactoryWithAuth.Token);

        var content = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/mcp", content).ConfigureAwait(false);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WithInvalidToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "wrong-token");

        var response = await client.PostAsync("/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json")).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WithNoAuthHeader_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json")).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

[ClassDataSource<McpHttpTestFactory>(Shared = SharedType.PerClass)]
public sealed class BearerAuthMiddlewareNoTokenTests(McpHttpTestFactory factory)
{
    [Test]
    public async Task Mcp_NoAuthConfig_AllowsAnonymous()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json")).ConfigureAwait(false);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
