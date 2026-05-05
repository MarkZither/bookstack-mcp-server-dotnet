using BookStack.Mcp.Server.Admin;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Admin;

public sealed class AdminHandlersUrlValidationTests
{
    private const string BaseUrl = "http://bookstack.example.com:8080";

    [Test]
    public void ValidatePageUrl_NullUrl_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl(null, BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_EmptyUrl_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl(string.Empty, BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_RelativeUrl_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl("/books/my-book/pages/my-page", BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_FileScheme_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl("file:///etc/passwd", BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_FtpScheme_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl("ftp://bookstack.example.com:8080/page", BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_MatchingHost_ReturnsNull()
    {
        var error = AdminHandlers.ValidatePageUrl(
            "http://bookstack.example.com:8080/books/test/pages/my-page",
            BaseUrl);

        error.Should().BeNull("a URL with matching host and port should pass validation");
    }

    [Test]
    public void ValidatePageUrl_MatchingHostHttps_ReturnsNull()
    {
        var error = AdminHandlers.ValidatePageUrl(
            "https://bookstack.example.com:8080/books/test/pages/my-page",
            BaseUrl);

        error.Should().BeNull("https scheme is also valid");
    }

    [Test]
    public void ValidatePageUrl_DifferentHost_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl(
            "http://evil.example.com/books/test/pages/my-page",
            BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_DifferentPort_ReturnsError()
    {
        var error = AdminHandlers.ValidatePageUrl(
            "http://bookstack.example.com:9999/books/test/pages/my-page",
            BaseUrl);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidatePageUrl_NullBaseUrl_AllowsAnyHost()
    {
        // When base URL is not configured, SSRF guard is skipped (fail-open
        // is acceptable in tests / unconfigured environments).
        var error = AdminHandlers.ValidatePageUrl("http://any.host/page", null);

        error.Should().BeNull("null base URL means no host restriction is applied");
    }
}
