using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Books;

[McpServerToolType]
internal sealed class BookToolHandler(IBookStackApiClient client, ILogger<BookToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<BookToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_books_list"), Description("List all books in BookStack")]
    public Task<string> ListBooksAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_read"), Description("Get a book by ID")]
    public Task<string> ReadBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_create"), Description("Create a new book")]
    public Task<string> CreateBookAsync(
        [Description("The book name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_update"), Description("Update an existing book")]
    public Task<string> UpdateBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_delete"), Description("Delete a book by ID")]
    public Task<string> DeleteBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_export"), Description("Export a book in a given format")]
    public Task<string> ExportBookAsync(
        [Description("The book ID")] int id,
        [Description("Export format: html, pdf, plaintext, markdown")] string format,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");
}
