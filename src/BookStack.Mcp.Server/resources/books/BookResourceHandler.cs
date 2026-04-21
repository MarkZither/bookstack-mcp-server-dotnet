using System.ComponentModel;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerResource(UriTemplate = "bookstack://books", Name = "Books")]
    [Description("All books visible to the authenticated user.")]
    public async Task<string> GetBooksAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _client.ListBooksAsync(null, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing books resource: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerResource(UriTemplate = "bookstack://books/{id}", Name = "Book")]
    [Description("A single book including its chapter and page hierarchy.")]
    public async Task<string> GetBookAsync(
        [Description("The book ID.")] int id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _client.GetBookAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error reading book resource {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
