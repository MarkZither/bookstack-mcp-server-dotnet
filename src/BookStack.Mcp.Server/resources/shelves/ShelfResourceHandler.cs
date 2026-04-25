using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Shelves;

[McpServerResourceType]
internal sealed class ShelfResourceHandler(
    IBookStackApiClient client,
    ILogger<ShelfResourceHandler> logger,
    IOptions<ScopeFilterOptions> scopeOptions)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ShelfResourceHandler> _logger = logger;
    private readonly IOptions<ScopeFilterOptions> _scopeOptions = scopeOptions;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerResource(UriTemplate = "bookstack://shelves", Name = "Shelves")]
    [Description("All bookshelves visible to the authenticated user.")]
    public async Task<string> GetShelvesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _client.ListShelvesAsync(null, ct).ConfigureAwait(false);
            var scope = _scopeOptions.Value;
            if (scope.HasShelfScope)
            {
                var filtered = result.Data
                    .Where(s => ScopeFilter.MatchesScope(s.Id, s.Slug, scope.ScopedShelves))
                    .ToList();
                result = new ListResponse<Bookshelf> { Total = filtered.Count, From = result.From, To = result.To, Data = filtered };
            }
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing shelves resource: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerResource(UriTemplate = "bookstack://shelves/{id}", Name = "Shelf")]
    [Description("A single bookshelf including its list of assigned books.")]
    public async Task<string> GetShelfAsync(
        [Description("The shelf ID.")] int id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _client.GetShelfAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error reading shelf resource {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
