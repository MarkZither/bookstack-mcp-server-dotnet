using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Search;

[McpServerToolType]
internal sealed class SearchToolHandler(IBookStackApiClient client, ILogger<SearchToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<SearchToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_search"), Description("Search for content across BookStack")]
    public Task<string> SearchAsync(
        [Description("The search query")] string query, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #12");
}
