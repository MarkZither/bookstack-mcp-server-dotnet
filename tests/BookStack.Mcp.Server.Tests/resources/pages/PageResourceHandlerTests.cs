using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Resources.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Resources.Pages;

public sealed class PageResourceHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly PageResourceHandler _handler;

    public PageResourceHandlerTests()
    {
        _handler = new PageResourceHandler(_client.Object, NullLogger<PageResourceHandler>.Instance);
    }

    [Test]
    public async Task GetPagesResource_ReturnsSerializedListResponse()
    {
        _client.Setup(c => c.ListPagesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page> { Data = [new Page { Id = 3 }], Total = 1 });

        var result = await _handler.GetPagesAsync().ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task GetPageByIdResource_ValidId_ReturnsSerializedPageWithContent()
    {
        _client.Setup(c => c.GetPageAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 3, Html = "<p>Content</p>" });

        var result = await _handler.GetPageAsync(3).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("html").GetString().Should().Be("<p>Content</p>");
    }
}
