using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Tools.Books;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Tools.Books;

public sealed class BookToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly BookToolHandler _handler;

    public BookToolHandlerTests()
    {
        _handler = new BookToolHandler(_client.Object, NullLogger<BookToolHandler>.Instance);
    }

    [Test]
    public async Task ListBooksAsync_NoParams_CallsClientWithNullQuery()
    {
        _client.Setup(c => c.ListBooksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Book>());

        await _handler.ListBooksAsync().ConfigureAwait(false);

        _client.Verify(c => c.ListBooksAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ListBooksAsync_WithCountAndOffset_PassesParams()
    {
        _client.Setup(c => c.ListBooksAsync(It.IsAny<ListQueryParams?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Book>());

        await _handler.ListBooksAsync(count: 5, offset: 10).ConfigureAwait(false);

        _client.Verify(
            c => c.ListBooksAsync(
                It.Is<ListQueryParams?>(q => q != null && q.Count == 5 && q.Offset == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ReadBookAsync_ValidId_ReturnsSerializedBook()
    {
        _client.Setup(c => c.GetBookAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 42 });

        var result = await _handler.ReadBookAsync(42).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task ReadBookAsync_NotFound_ReturnsErrorJson()
    {
        _client.Setup(c => c.GetBookAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookStackApiException(404, "Not found", null));

        var result = await _handler.ReadBookAsync(999).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Test]
    public async Task CreateBookAsync_ValidName_ReturnsSerializedBook()
    {
        _client.Setup(c => c.CreateBookAsync(It.IsAny<CreateBookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 1, Name = "Test" });

        var result = await _handler.CreateBookAsync("Test").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Test");
        _client.Verify(
            c => c.CreateBookAsync(
                It.Is<CreateBookRequest>(r => r.Name == "Test"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CreateBookAsync_WithTags_PassesTagsToRequest()
    {
        _client.Setup(c => c.CreateBookAsync(It.IsAny<CreateBookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 1, Name = "T" });

        await _handler.CreateBookAsync("T", tags: [new Tag { Name = "env", Value = "prod" }]).ConfigureAwait(false);

        _client.Verify(
            c => c.CreateBookAsync(
                It.Is<CreateBookRequest>(r => r.Tags != null && r.Tags.Count == 1 && r.Tags[0].Name == "env"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task UpdateBookAsync_ValidId_ReturnsUpdatedBook()
    {
        _client.Setup(c => c.UpdateBookAsync(5, It.IsAny<UpdateBookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Book { Id = 5, Name = "Updated" });

        var result = await _handler.UpdateBookAsync(5, name: "Updated").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Updated");
    }

    [Test]
    public async Task DeleteBookAsync_ValidId_ReturnsSuccessJson()
    {
        _client.Setup(c => c.DeleteBookAsync(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.DeleteBookAsync(3).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("3");
    }

    [Test]
    public async Task ExportBookAsync_InvalidFormat_ReturnsValidationError()
    {
        var result = await _handler.ExportBookAsync(1, "docx").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
        _client.Verify(c => c.ExportBookAsync(It.IsAny<int>(), It.IsAny<ExportFormat>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExportBookAsync_ValidFormatMarkdown_ReturnsExportString()
    {
        _client.Setup(c => c.ExportBookAsync(1, ExportFormat.Markdown, It.IsAny<CancellationToken>()))
            .ReturnsAsync("# My Book");

        var result = await _handler.ExportBookAsync(1, "markdown").ConfigureAwait(false);

        result.Should().Be("# My Book");
    }
}
