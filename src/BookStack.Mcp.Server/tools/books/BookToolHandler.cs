using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Books;

[McpServerToolType]
internal sealed class BookToolHandler(IBookStackApiClient client, ILogger<BookToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<BookToolHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_books_list")]
    [Description("List all books visible to the authenticated user with pagination options. Books are the top-level containers in the BookStack content hierarchy.")]
    public async Task<string> ListBooksAsync(
        [Description("Number of books to return (1–500). Defaults to 20.")] int? count = null,
        [Description("Number of books to skip for pagination. Defaults to 0.")] int? offset = null,
        [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = (count.HasValue || offset.HasValue || sort is not null)
                ? new ListQueryParams { Count = count, Offset = offset, Sort = sort }
                : null;
            var result = await _client.ListBooksAsync(query, ct).ConfigureAwait(false);
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
            _logger.LogError(ex, "BookStack API error listing books: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_books_read")]
    [Description("Get a single book by ID, including its full chapter and page hierarchy.")]
    public async Task<string> ReadBookAsync(
        [Description("The book ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

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
            _logger.LogError(ex, "BookStack API error reading book {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_books_create")]
    [Description("Create a new book. Books are the top-level containers for chapters and pages in BookStack.")]
    public async Task<string> CreateBookAsync(
        [Description("The book name (required, max 255 characters).")] string name,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Each object must have name and value string properties. On update, providing this array replaces ALL existing tags.")] IList<Tag>? tags = null,
        [Description("ID of a page to use as the default template for new pages in this book.")] int? defaultTemplateId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CreateBookRequest
            {
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
                DefaultTemplateId = defaultTemplateId,
            };
            var result = await _client.CreateBookAsync(request, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 422)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error creating book: {Message}", ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_books_update")]
    [Description("Update an existing book's name, description, or tags.")]
    public async Task<string> UpdateBookAsync(
        [Description("The book ID. Must be a positive integer.")] int id,
        [Description("New name for the book (max 255 characters).")] string? name = null,
        [Description("Plain text description (max 1900 characters).")] string? description = null,
        [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
        [Description("Tags to assign. Providing this array replaces ALL existing tags. Omit to leave tags unchanged.")] IList<Tag>? tags = null,
        [Description("ID of a page to use as the default template for new pages in this book.")] int? defaultTemplateId = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            var request = new UpdateBookRequest
            {
                Name = name,
                Description = description,
                DescriptionHtml = descriptionHtml,
                Tags = tags,
                DefaultTemplateId = defaultTemplateId,
            };
            var result = await _client.UpdateBookAsync(id, request, ct).ConfigureAwait(false);
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
            _logger.LogError(ex, "BookStack API error updating book {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_books_delete")]
    [Description("Delete a book by ID. This moves the book and all its contents to the recycle bin.")]
    public async Task<string> DeleteBookAsync(
        [Description("The book ID. Must be a positive integer.")] int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
        }

        try
        {
            await _client.DeleteBookAsync(id, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, message = $"Book {id} deleted successfully" }, _jsonOptions);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error deleting book {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }

    [McpServerTool(Name = "bookstack_books_export")]
    [Description("Export a book in the specified format. Returns the raw export content as a string.")]
    public async Task<string> ExportBookAsync(
        [Description("The book ID. Must be a positive integer.")] int id,
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
            return await _client.ExportBookAsync(id, exportFormat, ct).ConfigureAwait(false);
        }
        catch (BookStackApiException ex) when (ex.StatusCode == 404)
        {
            return JsonSerializer.Serialize(new { error = "not_found", message = ex.ErrorMessage }, _jsonOptions);
        }
        catch (BookStackApiException ex)
        {
            _logger.LogError(ex, "BookStack API error exporting book {Id}: {Message}", id, ex.Message);
            return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
        }
    }
}
