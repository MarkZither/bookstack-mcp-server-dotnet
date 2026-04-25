using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Search;

[McpServerToolType]
internal sealed class SearchToolHandler(
    IBookStackApiClient client,
    ILogger<SearchToolHandler> logger,
    IOptions<ScopeFilterOptions> scopeOptions)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<SearchToolHandler> _logger = logger;
    private readonly IOptions<ScopeFilterOptions> _scopeOptions = scopeOptions;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_search")]
    [Description("Search across all BookStack content (pages, books, chapters, shelves). Supports advanced query syntax: \"exact phrase\", {type:page|book|chapter|shelf}, {tag:name=value}, {created_by:me}. Note: page results contain snippets only — use bookstack_pages_read to retrieve full content.")]
    public async Task<string> SearchAsync(
        [Description("Search query string. Required. Supports advanced syntax filters.")] string query,
        [Description("Page number for pagination. Minimum 1. Defaults to 1.")] int? page = null,
        [Description("Results per page. Range 1–100. Defaults to 20.")] int? count = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(
                new { error = "validation_error", message = "query is required and cannot be empty." },
                _jsonOptions);
        }

        try
        {
            var request = new SearchRequest
            {
                Query = query,
                Page = page,
                Count = count,
            };
            var result = await _client.SearchAsync(request, ct).ConfigureAwait(false);
            var scope = _scopeOptions.Value;
            if (scope.HasBookScope)
            {
                var filtered = result.Data
                    .Where(item =>
                        item.Type == "book"
                            ? ScopeFilter.MatchesScope(item.Id, item.Slug, scope.ScopedBooks)
                            : item.Book is not null && ScopeFilter.MatchesScope(item.Book.Id, item.Book.Slug, scope.ScopedBooks))
                    .ToList();
                result = new SearchResult { Total = filtered.Count, From = result.From, To = result.To, Data = filtered };
            }
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error searching content: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
