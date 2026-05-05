using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Admin;

[ClassDataSource<AdminSidecarTestFactory>(Shared = SharedType.PerClass)]
public sealed class AdminStatusEndpointTests(AdminSidecarTestFactory factory)
{
    [Test]
    public async Task Status_Returns200_WithCorrectSchema()
    {
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/admin/status").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.TryGetProperty("totalPages", out _).Should().BeTrue("totalPages must be present");
        root.TryGetProperty("pendingCount", out _).Should().BeTrue("pendingCount must be present");
        root.TryGetProperty("lastSyncTime", out _).Should().BeTrue("lastSyncTime must be present");
    }

    [Test]
    public async Task Status_NeverSynced_ReturnsNullLastSyncTimeAndZeroTotal()
    {
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/admin/status").ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("totalPages").GetInt32().Should().Be(0);
        root.GetProperty("pendingCount").GetInt32().Should().Be(0);
        root.GetProperty("lastSyncTime").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
