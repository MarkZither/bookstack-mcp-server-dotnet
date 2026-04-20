using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Books;

[McpServerResourceType]
internal sealed class BookResourceHandler(
    IBookStackApiClient client, ILogger<BookResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<BookResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://books", Name = "Books")]
    [Description("All books in the BookStack instance")]
    public Task<string> GetBooksAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
