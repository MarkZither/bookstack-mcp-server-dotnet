using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Tools.Shelves;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookStack.Mcp.Server.Tests.Tools.Shelves;

public sealed class ShelfToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly ShelfToolHandler _handler;

    public ShelfToolHandlerTests()
    {
        _handler = new ShelfToolHandler(_client.Object, NullLogger<ShelfToolHandler>.Instance);
    }

    [Test]
    public async Task CreateShelfAsync_WithBooks_SetsBooksList()
    {
        _client.Setup(c => c.CreateShelfAsync(It.IsAny<CreateShelfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Bookshelf { Id = 4, Name = "My Shelf" });

        await _handler.CreateShelfAsync("My Shelf", books: [1, 2, 3]).ConfigureAwait(false);

        _client.Verify(
            c => c.CreateShelfAsync(
                It.Is<CreateShelfRequest>(r => r.Books != null && r.Books.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task UpdateShelfAsync_WithBooks_ReplacesExistingBooks()
    {
        _client.Setup(c => c.UpdateShelfAsync(4, It.IsAny<UpdateShelfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Bookshelf { Id = 4 });

        await _handler.UpdateShelfAsync(4, books: [5]).ConfigureAwait(false);

        _client.Verify(
            c => c.UpdateShelfAsync(
                4,
                It.Is<UpdateShelfRequest>(r => r.Books != null && r.Books.Count == 1 && r.Books[0] == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task DeleteShelfAsync_ValidId_ReturnsSuccessJson()
    {
        _client.Setup(c => c.DeleteShelfAsync(4, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.DeleteShelfAsync(4).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("4");
    }
}
