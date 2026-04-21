using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Tools.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Tools.Pages;

public sealed class PageToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly PageToolHandler _handler;

    public PageToolHandlerTests()
    {
        _handler = new PageToolHandler(_client.Object, NullLogger<PageToolHandler>.Instance);
    }

    [Test]
    public async Task CreatePageAsync_NoParentId_ReturnsValidationError()
    {
        var result = await _handler.CreatePageAsync("Page1").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
        var message = doc.RootElement.GetProperty("message").GetString();
        (message!.Contains("bookId") || message.Contains("chapterId")).Should().BeTrue();
        _client.Verify(c => c.CreatePageAsync(It.IsAny<CreatePageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreatePageAsync_WithBookId_CallsClientWithCorrectRequest()
    {
        _client.Setup(c => c.CreatePageAsync(It.IsAny<CreatePageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Page { Id = 5, BookId = 3 });

        var result = await _handler.CreatePageAsync("Page1", bookId: 3).ConfigureAwait(false);

        JsonDocument.Parse(result); // validate JSON
        _client.Verify(
            c => c.CreatePageAsync(
                It.Is<CreatePageRequest>(r => r.BookId == 3 && r.Name == "Page1" && r.ChapterId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CreatePageAsync_WithMarkdown_SetsMarkdownField()
    {
        _client.Setup(c => c.CreatePageAsync(It.IsAny<CreatePageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Page { Id = 6 });

        await _handler.CreatePageAsync("Md Page", chapterId: 1, markdown: "# Hello").ConfigureAwait(false);

        _client.Verify(
            c => c.CreatePageAsync(
                It.Is<CreatePageRequest>(r => r.Markdown == "# Hello" && r.Html == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ReadPageAsync_ValidId_ReturnsPageWithContent()
    {
        _client.Setup(c => c.GetPageAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 8, Html = "<p>Hi</p>" });

        var result = await _handler.ReadPageAsync(8).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("html").GetString().Should().Be("<p>Hi</p>");
    }

    [Test]
    public async Task DeletePageAsync_NotFound_ReturnsErrorJson()
    {
        _client.Setup(c => c.DeletePageAsync(99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookStackApiException(404, "Not found", null));

        var result = await _handler.DeletePageAsync(99).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }
}
