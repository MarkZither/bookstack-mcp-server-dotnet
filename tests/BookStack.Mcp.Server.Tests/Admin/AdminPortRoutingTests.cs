using System.Net;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Admin;

[ClassDataSource<AdminSidecarTestFactory>(Shared = SharedType.PerClass)]
public sealed class AdminPortRoutingTests(AdminSidecarTestFactory factory)
{
    /// <summary>
    /// Admin routes must return 404 when accessed without the admin Host header
    /// (i.e., as if routed through the MCP listener).
    /// </summary>
    [Test]
    public async Task AdminRoutes_NotAccessibleOnMcpPort()
    {
        // Use a plain client — no Host override — simulating MCP-port access.
        var client = factory.CreateClient();

        var response = await client.GetAsync("/admin/status").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "RequireHost constraint must block admin endpoints on non-admin listeners");
    }
}

[ClassDataSource<AdminDisabledTestFactory>(Shared = SharedType.PerClass)]
public sealed class AdminDisabledRoutingTests(AdminDisabledTestFactory factory)
{
    /// <summary>
    /// When BOOKSTACK_ADMIN_PORT=0, admin routes are not registered and all
    /// requests to /admin/* return 404.
    /// </summary>
    [Test]
    public async Task AdminPort0_SidecarNotRegistered_Returns404()
    {
        var client = factory.CreateClient();

        var statusResponse = await client.GetAsync("/admin/status").ConfigureAwait(false);
        var syncResponse = await client.PostAsync("/admin/sync", content: null).ConfigureAwait(false);

        statusResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        syncResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AdminPort0_CustomAdminHostHeader_StillReturns404()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "127.0.0.1:5175";

        var response = await client.GetAsync("/admin/status").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "routes must not exist at all when adminPort=0");
    }
}
