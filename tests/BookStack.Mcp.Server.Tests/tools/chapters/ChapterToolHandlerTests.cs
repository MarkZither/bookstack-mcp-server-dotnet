using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Tools.Chapters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Tools.Chapters;

public sealed class ChapterToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly ChapterToolHandler _handler;

    public ChapterToolHandlerTests()
    {
        _handler = new ChapterToolHandler(_client.Object, NullLogger<ChapterToolHandler>.Instance);
    }

    [Test]
    public async Task CreateChapterAsync_ValidBookIdAndName_ReturnsSerializedChapter()
    {
        _client.Setup(c => c.CreateChapterAsync(It.IsAny<CreateChapterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chapter { Id = 10, BookId = 2, Name = "Ch1" });

        var result = await _handler.CreateChapterAsync(bookId: 2, name: "Ch1").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("bookId").GetInt32().Should().Be(2);
        _client.Verify(
            c => c.CreateChapterAsync(
                It.Is<CreateChapterRequest>(r => r.BookId == 2 && r.Name == "Ch1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ReadChapterAsync_NotFound_ReturnsErrorJson()
    {
        _client.Setup(c => c.GetChapterAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookStackApiException(404, "Not found", null));

        var result = await _handler.ReadChapterAsync(999).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Test]
    public async Task DeleteChapterAsync_ValidId_ReturnsSuccessJson()
    {
        _client.Setup(c => c.DeleteChapterAsync(7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.DeleteChapterAsync(7).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("7");
    }
}
