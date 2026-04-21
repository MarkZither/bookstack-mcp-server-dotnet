using System.ComponentModel;
using System.Text.Json;
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
