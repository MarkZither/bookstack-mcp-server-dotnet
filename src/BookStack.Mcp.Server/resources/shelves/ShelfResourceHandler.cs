using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Shelves;

[McpServerResourceType]
internal sealed class ShelfResourceHandler(
    IBookStackApiClient client, ILogger<ShelfResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ShelfResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://shelves", Name = "Shelves")]
    [Description("All shelves in the BookStack instance")]
    public Task<string> GetShelvesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
