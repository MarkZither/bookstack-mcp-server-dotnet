using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Admin;

[ClassDataSource<AdminSidecarTestFactory>(Shared = SharedType.PerClass)]
public sealed class AdminSyncEndpointTests(AdminSidecarTestFactory factory)
{
    [Test]
    public async Task Sync_Returns202_WithAcceptedStatus()
    {
        var client = factory.CreateAdminClient();

        var response = await client.PostAsync("/admin/sync", content: null).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("accepted");
    }
}
