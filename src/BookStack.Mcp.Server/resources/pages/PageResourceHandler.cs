using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Pages;

[McpServerResourceType]
internal sealed class PageResourceHandler(
    IBookStackApiClient client, ILogger<PageResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<PageResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://pages", Name = "Pages")]
    [Description("All pages in the BookStack instance")]
    public Task<string> GetPagesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
