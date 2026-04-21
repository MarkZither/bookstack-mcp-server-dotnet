using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Resources.Search;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Resources.Search;

public sealed class SearchResourceHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly SearchResourceHandler _handler;

    public SearchResourceHandlerTests()
    {
        _handler = new SearchResourceHandler(_client.Object, NullLogger<SearchResourceHandler>.Instance);
    }

    [Test]
    public async Task GetSearchAsync_ValidQuery_ReturnsSerializedResult()
    {
        var item = new SearchResultItem { Id = 3, Name = "API Docs", Type = "page" };
        _client.Setup(c => c.SearchAsync(
                It.Is<SearchRequest>(r => r.Query == "API"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Total = 1, Data = [item] });

        var result = await _handler.GetSearchAsync("API").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("data")[0].GetProperty("name").GetString().Should().Be("API Docs");
    }

    [Test]
    public async Task GetSearchAsync_ApiException_ReturnsErrorJson()
    {
        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookStackApiException(503, "Service unavailable", null));

        var result = await _handler.GetSearchAsync("anything").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("api_error");
    }
}
