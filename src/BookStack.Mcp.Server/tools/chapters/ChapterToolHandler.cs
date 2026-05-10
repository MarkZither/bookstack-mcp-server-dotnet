using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Chapters;

[McpServerToolType]
internal sealed class ChapterToolHandler(IBookStackApiClient client, ILogger<ChapterToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ChapterToolHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_chapters_list")]
    [Description("List all chapters visible to the authenticated user with pagination options.")]
    public async Task<string> ListChaptersAsync(
        [Description("Number of chapters to return (1–500). Defaults to 20.")] int? count = null,
        [Description("Number of chapters to skip for pagination. Defaults to 0.")] int? offset = null,
        [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = (count.HasValue || offset.HasValue || sort is not null)
                ? new ListQueryParams { Count = count, Offset = offset, Sort = sort }
                : null;
            var result = await _client.ListChaptersAsync(query, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage, validation = ex.ValidationErrors }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing chapters: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_chapters_read")]
    [Description("Get a single chapter by ID, including its list of pages.")]
    public async Task<string> ReadChapterAsync(
        [Description("The chapter ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

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
            _logger.LogError(ex, "BookStack API error reading chapter {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_chapters_create")]
    [Description("Create a new chapter inside a book.")]
    public async Task<string> CreateChapterAsync(
        [Description("ID of the book that will contain this chapter. Must be a positive integer.")] int bookId,
        [Description("The chapter name (required, max 255 characters).")] string name,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Each object must have name and value string properties.")] IList<Tag>? tags = null,
        CancellationToken ct = default)
    {
        if (bookId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"bookId must be a positive integer, got {bookId}." }, _jsonOptions);
        }

        try
        {
            var request = new CreateChapterRequest
            {
                BookId = bookId,
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
            };
            var result = await _client.CreateChapterAsync(request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage, validation = ex.ValidationErrors }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error creating chapter: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_chapters_update")]
    [Description("Update an existing chapter's name, description, tags, or move it to a different book.")]
    public async Task<string> UpdateChapterAsync(
        [Description("The chapter ID. Must be a positive integer.")] int id,
        [Description("Move the chapter to a different book by specifying the target book ID.")] int? bookId = null,
        [Description("New name for the chapter (max 255 characters).")] string? name = null,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Providing this array replaces ALL existing tags. Omit to leave tags unchanged.")] IList<Tag>? tags = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        if (bookId.HasValue && bookId.Value <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"bookId must be a positive integer, got {bookId}." }, _jsonOptions);
        }

        try
        {
            var request = new UpdateChapterRequest
            {
                BookId = bookId,
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
            };
            var result = await _client.UpdateChapterAsync(id, request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage, validation = ex.ValidationErrors }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error updating chapter {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_chapters_delete")]
    [Description("Delete a chapter by ID. This moves the chapter and all its pages to the recycle bin.")]
    public async Task<string> DeleteChapterAsync(
        [Description("The chapter ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            await _client.DeleteChapterAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, message = $"Chapter {id} deleted successfully" }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error deleting chapter {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_chapters_export")]
    [Description("Export a chapter in the specified format. Returns the raw export content as a string.")]
    public async Task<string> ExportChapterAsync(
        [Description("The chapter ID. Must be a positive integer.")] int id,
        [Description("Export format. Must be one of: html, pdf, plaintext, markdown.")] string format,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        if (!Enum.TryParse<ExportFormat>(format, ignoreCase: true, out var exportFormat))
        {
            return JsonSerializer.Serialize(
                new { error = "validation_error", message = $"Invalid export format '{format}'. Must be one of: html, pdf, plaintext, markdown." },
                _jsonOptions);
        }

        try
        {
            return await _client.ExportChapterAsync(id, exportFormat, ct).ConfigureAwait(false);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error exporting chapter {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
