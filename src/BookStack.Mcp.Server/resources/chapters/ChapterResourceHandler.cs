using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Chapters;

[McpServerResourceType]
internal sealed class ChapterResourceHandler(
    IBookStackApiClient client, ILogger<ChapterResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ChapterResourceHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerResource(UriTemplate = "bookstack://chapters", Name = "Chapters")]
    [Description("All chapters visible to the authenticated user.")]
    public async Task<string> GetChaptersAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _client.ListChaptersAsync(null, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing chapters resource: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerResource(UriTemplate = "bookstack://chapters/{id}", Name = "Chapter")]
    [Description("A single chapter including its list of pages.")]
    public async Task<string> GetChapterAsync(
        [Description("The chapter ID.")] int id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _client.GetChapterAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error reading chapter resource {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
