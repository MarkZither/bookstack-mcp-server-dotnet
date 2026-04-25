using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Tools.Search;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BookStack.Mcp.Server.Tests.Tools.Search;

public sealed class SearchToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly SearchToolHandler _handler;

    public SearchToolHandlerTests()
    {
        _handler = new SearchToolHandler(
            _client.Object,
            NullLogger<SearchToolHandler>.Instance,
            Options.Create(new ScopeFilterOptions()));
    }

    [Test]
    public async Task SearchAsync_EmptyQuery_ReturnsValidationError()
    {
        var result = await _handler.SearchAsync("   ").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
        _client.Verify(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SearchAsync_ValidQuery_CallsClientWithQuery()
    {
        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Total = 0, Data = [] });

        await _handler.SearchAsync("bookstack").ConfigureAwait(false);

        _client.Verify(
            c => c.SearchAsync(
                It.Is<SearchRequest>(r => r.Query == "bookstack" && r.Page == null && r.Count == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SearchAsync_WithPageAndCount_PassesParams()
    {
        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Total = 0, Data = [] });

        await _handler.SearchAsync("API", page: 2, count: 10).ConfigureAwait(false);

        _client.Verify(
            c => c.SearchAsync(
                It.Is<SearchRequest>(r => r.Query == "API" && r.Page == 2 && r.Count == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SearchAsync_ReturnsSerializedResult()
    {
        var item = new SearchResultItem { Id = 5, Name = "My Page", Type = "page" };
        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Total = 1, Data = [item] });

        var result = await _handler.SearchAsync("My Page").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("data")[0].GetProperty("id").GetInt32().Should().Be(5);
    }

    [Test]
    public async Task SearchAsync_ApiException_ReturnsErrorJson()
    {
        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookStackApiException(500, "Internal error", null));

        var result = await _handler.SearchAsync("query").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("api_error");
    }

    [Test]
    public async Task SearchAsync_WithBookScope_FiltersBookItems()
    {
        var scopedHandler = new SearchToolHandler(
            _client.Object,
            NullLogger<SearchToolHandler>.Instance,
            Options.Create(new ScopeFilterOptions { ScopedBooks = ["allowed-book"] }));

        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Total = 3,
                Data = [
                    new SearchResultItem { Id = 1, Slug = "allowed-book",  Type = "book" },
                    new SearchResultItem { Id = 2, Slug = "other-book",    Type = "book" },
                    new SearchResultItem { Id = 3, Slug = "page-in-allowed", Type = "page",
                        Book = new Book { Id = 1, Slug = "allowed-book" } },
                ],
            });

        var json = await scopedHandler.SearchAsync("test").ConfigureAwait(false);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(2);
    }

    [Test]
    public async Task SearchAsync_WithBookScope_ExcludesPageWithNoBook()
    {
        var scopedHandler = new SearchToolHandler(
            _client.Object,
            NullLogger<SearchToolHandler>.Instance,
            Options.Create(new ScopeFilterOptions { ScopedBooks = ["allowed-book"] }));

        _client.Setup(c => c.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Total = 1,
                Data = [new SearchResultItem { Id = 5, Slug = "orphan-page", Type = "page", Book = null }],
            });

        var json = await scopedHandler.SearchAsync("test").ConfigureAwait(false);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }
}
