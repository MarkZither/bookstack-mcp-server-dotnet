using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Resources.Books;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Resources.Books;

public sealed class BookResourceHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly BookResourceHandler _handler;

    public BookResourceHandlerTests()
    {
        _handler = new BookResourceHandler(_client.Object, NullLogger<BookResourceHandler>.Instance);
    }

    [Test]
    public async Task GetBooksResource_ReturnsSerializedListResponse()
    {
        _client.Setup(c => c.ListBooksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Book> { Data = [new Book { Id = 1 }], Total = 1 });

        var result = await _handler.GetBooksAsync().ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
    }

    [Test]
    public async Task GetBookByIdResource_ValidId_ReturnsSerializedBookWithContents()
    {
        _client.Setup(c => c.GetBookAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 7 });

        var result = await _handler.GetBookAsync(7).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetInt32().Should().Be(7);
    }
}
