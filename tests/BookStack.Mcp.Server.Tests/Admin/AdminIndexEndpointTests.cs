using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Admin;

[ClassDataSource<AdminSidecarTestFactory>(Shared = SharedType.PerClass)]
public sealed class AdminIndexEndpointTests(AdminSidecarTestFactory factory)
{
    // The base URL matches the configured BOOKSTACK_BASE_URL in the factory.
    private const string ValidUrl = "http://fake.bookstack.test/books/my-book/pages/my-page";

    [Test]
    public async Task Index_WithValidUrl_Returns202()
    {
        var client = factory.CreateAdminClient();

        var response = await client
            .PostAsJsonAsync("/admin/index", new { url = ValidUrl })
            .ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("accepted");
    }

    [Test]
    public async Task Index_WithInvalidUrl_NotAbsolute_Returns400()
    {
        var client = factory.CreateAdminClient();

        var response = await client
            .PostAsJsonAsync("/admin/index", new { url = "/relative/path" })
            .ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorResponseAsync(response).ConfigureAwait(false);
    }

    [Test]
    public async Task Index_WithSchemeFile_Returns400()
    {
        var client = factory.CreateAdminClient();

        var response = await client
            .PostAsJsonAsync("/admin/index", new { url = "file:///etc/passwd" })
            .ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorResponseAsync(response).ConfigureAwait(false);
    }

    [Test]
    public async Task Index_WithUrlFromDifferentHost_Returns400()
    {
        var client = factory.CreateAdminClient();

        var response = await client
            .PostAsJsonAsync("/admin/index", new { url = "http://evil.example.com/page" })
            .ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorResponseAsync(response).ConfigureAwait(false);
    }

    [Test]
    public async Task Index_WithMissingUrlField_Returns400()
    {
        var client = factory.CreateAdminClient();

        // url field is null / absent
        var response = await client
            .PostAsJsonAsync("/admin/index", new { url = (string?)null })
            .ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorResponseAsync(response).ConfigureAwait(false);
    }

    [Test]
    public async Task Index_WithEmptyBody_Returns400()
    {
        var client = factory.CreateAdminClient();

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/admin/index", content).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorResponseAsync(response).ConfigureAwait(false);
    }

    private static async Task AssertErrorResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue("error field must be present");
    }
}
