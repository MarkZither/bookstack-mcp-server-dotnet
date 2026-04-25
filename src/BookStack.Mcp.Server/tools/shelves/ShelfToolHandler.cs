using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Shelves;

[McpServerToolType]
internal sealed class ShelfToolHandler(
    IBookStackApiClient client,
    ILogger<ShelfToolHandler> logger,
    IOptions<ScopeFilterOptions> scopeOptions)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ShelfToolHandler> _logger = logger;
    private readonly IOptions<ScopeFilterOptions> _scopeOptions = scopeOptions;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_shelves_list")]
    [Description("List all bookshelves visible to the authenticated user with pagination options. Shelves group related books into collections.")]
    public async Task<string> ListShelvesAsync(
        [Description("Number of shelves to return (1–500). Defaults to 20.")] int? count = null,
        [Description("Number of shelves to skip for pagination. Defaults to 0.")] int? offset = null,
        [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = (count.HasValue || offset.HasValue || sort is not null)
                ? new ListQueryParams { Count = count, Offset = offset, Sort = sort }
                : null;
            var result = await _client.ListShelvesAsync(query, ct).ConfigureAwait(false);
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
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing shelves: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_shelves_read")]
    [Description("Get a single bookshelf by ID, including the list of books assigned to it.")]
    public async Task<string> ReadShelfAsync(
        [Description("The shelf ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

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
            _logger.LogError(ex, "BookStack API error reading shelf {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_shelves_create")]
    [Description("Create a new bookshelf. Optionally assign books to the shelf at creation time.")]
    public async Task<string> CreateShelfAsync(
        [Description("The shelf name (required, max 255 characters).")] string name,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Each object must have name and value string properties.")] IList<Tag>? tags = null,
        [Description("List of book IDs to assign to this shelf. Providing this array on update replaces ALL currently assigned books.")] IList<int>? books = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CreateShelfRequest
            {
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
                Books = books,
            };
            var result = await _client.CreateShelfAsync(request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error creating shelf: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_shelves_update")]
    [Description("Update an existing shelf's name, description, tags, or assigned books.")]
    public async Task<string> UpdateShelfAsync(
        [Description("The shelf ID. Must be a positive integer.")] int id,
        [Description("New name for the shelf (max 255 characters).")] string? name = null,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Providing this array replaces ALL existing tags. Omit to leave tags unchanged.")] IList<Tag>? tags = null,
        [Description("List of book IDs to assign to this shelf. Providing this array replaces ALL currently assigned books. Omit to leave books unchanged.")] IList<int>? books = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            var request = new UpdateShelfRequest
            {
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
                Books = books,
            };
            var result = await _client.UpdateShelfAsync(id, request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error updating shelf {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_shelves_delete")]
    [Description("Delete a bookshelf by ID. The shelf's assigned books are NOT deleted.")]
    public async Task<string> DeleteShelfAsync(
        [Description("The shelf ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            await _client.DeleteShelfAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, message = $"Shelf {id} deleted successfully" }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error deleting shelf {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
