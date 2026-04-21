using System.ComponentModel;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerResource(UriTemplate = "bookstack://pages", Name = "Pages")]
    [Description("All pages visible to the authenticated user.")]
    public async Task<string> GetPagesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _client.ListPagesAsync(null, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing pages resource: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerResource(UriTemplate = "bookstack://pages/{id}", Name = "Page")]
    [Description("A single page including its HTML and Markdown content.")]
    public async Task<string> GetPageAsync(
        [Description("The page ID.")] int id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _client.GetPageAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error reading page resource {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
