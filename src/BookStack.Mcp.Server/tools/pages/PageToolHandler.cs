using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Pages;

[McpServerToolType]
internal sealed class PageToolHandler(IBookStackApiClient client, ILogger<PageToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<PageToolHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_pages_list")]
    [Description("List all pages visible to the authenticated user with pagination options.")]
    public async Task<string> ListPagesAsync(
        [Description("Number of pages to return (1–500). Defaults to 20.")] int? count = null,
        [Description("Number of pages to skip for pagination. Defaults to 0.")] int? offset = null,
        [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = (count.HasValue || offset.HasValue || sort is not null)
                ? new ListQueryParams { Count = count, Offset = offset, Sort = sort }
                : null;
            var result = await _client.ListPagesAsync(query, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error listing pages: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_pages_read")]
    [Description("Get a single page by ID, including its HTML and Markdown content.")]
    public async Task<string> ReadPageAsync(
        [Description("The page ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

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
            _logger.LogError(ex, "BookStack API error reading page {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_pages_create")]
    [Description("Create a new page. Either bookId or chapterId must be provided to specify where the page is created.")]
    public async Task<string> CreatePageAsync(
        [Description("The page name (required, max 255 characters).")] string name,
        [Description("ID of the book to create the page in. Required if chapterId is not provided.")] int? bookId = null,
        [Description("ID of the chapter to create the page in. Required if bookId is not provided.")] int? chapterId = null,
        [Description("HTML content for the page.")] string? html = null,
        [Description("Markdown content for the page. If both html and markdown are provided, html takes precedence.")] string? markdown = null,
        [Description("Tags to assign. Each object must have name and value string properties.")] IList<Tag>? tags = null,
        CancellationToken ct = default)
    {
        if (bookId is null && chapterId is null)
        {
            return JsonSerializer.Serialize(
                new { error = "validation_error", message = "Either bookId or chapterId is required." },
                _jsonOptions);
        }
        try
        {
            var request = new CreatePageRequest
            {
                Name = name,
                BookId = bookId,
                ChapterId = chapterId,
                Html = html,
                Markdown = markdown,
                Tags = tags,
            };
            var result = await _client.CreatePageAsync(request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error creating page: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_pages_update")]
    [Description("Update an existing page's name, content, tags, or move it to a different book or chapter.")]
    public async Task<string> UpdatePageAsync(
        [Description("The page ID. Must be a positive integer.")] int id,
        [Description("New name for the page (max 255 characters).")] string? name = null,
        [Description("Move the page to a different book.")] int? bookId = null,
        [Description("Move the page to a different chapter.")] int? chapterId = null,
        [Description("HTML content for the page.")] string? html = null,
        [Description("Markdown content for the page.")] string? markdown = null,
        [Description("Tags to assign. Providing this array replaces ALL existing tags. Omit to leave tags unchanged.")] IList<Tag>? tags = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            var request = new UpdatePageRequest
            {
                Name = name,
                BookId = bookId,
                ChapterId = chapterId,
                Html = html,
                Markdown = markdown,
                Tags = tags,
            };
            var result = await _client.UpdatePageAsync(id, request, ct).ConfigureAwait(false);
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
            _logger.LogError(ex, "BookStack API error updating page {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_pages_delete")]
    [Description("Delete a page by ID. This moves the page to the recycle bin.")]
    public async Task<string> DeletePageAsync(
        [Description("The page ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            await _client.DeletePageAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, message = $"Page {id} deleted successfully" }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error deleting page {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_pages_export")]
    [Description("Export a page in the specified format. Returns the raw export content as a string.")]
    public async Task<string> ExportPageAsync(
        [Description("The page ID. Must be a positive integer.")] int id,
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
            return await _client.ExportPageAsync(id, exportFormat, ct).ConfigureAwait(false);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error exporting page {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
