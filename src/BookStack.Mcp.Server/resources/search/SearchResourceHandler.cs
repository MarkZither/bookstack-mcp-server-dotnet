using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Search;

[McpServerResourceType]
internal sealed class SearchResourceHandler(
    IBookStackApiClient client, ILogger<SearchResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<SearchResourceHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerResource(UriTemplate = "bookstack://search/{query}", Name = "Search")]
    [Description("Search results for a given query across all BookStack content.")]
    public async Task<string> GetSearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var request = new SearchRequest { Query = query };
            var result = await _client.SearchAsync(request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error searching content: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
