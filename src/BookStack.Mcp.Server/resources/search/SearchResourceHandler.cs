using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Search;

[McpServerResourceType]
internal sealed class SearchResourceHandler(
    IBookStackApiClient client, ILogger<SearchResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<SearchResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://search", Name = "Search")]
    [Description("Search results from the BookStack instance")]
    public Task<string> GetSearchAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
