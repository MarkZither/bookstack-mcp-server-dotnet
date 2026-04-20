# Implementation Plan: Content Tools and Resources — Books, Chapters, Pages, and Shelves CRUD

**Feature**: FEAT-0007
**Spec**: [docs/features/content-tools/spec.md](spec.md)
**GitHub Issue**: [#7](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/7)
**Status**: Ready for implementation

---

## Architecture Decisions

All ADRs for this feature have been accepted. Implementation must follow them without deviation.

| ADR | Title | Decision Summary |
| --- | --- | --- |
| [ADR-0001](../../architecture/decisions/ADR-0001-mcp-sdk-selection.md) | MCP SDK Selection | Use `ModelContextProtocol` 1.2.0; attribute-based tool and resource discovery |
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution and Project Structure | `tools/<entity>/` and `resources/<entity>/` subdirectories; namespaces mirror paths |
| [ADR-0005](../../architecture/decisions/ADR-0005-ihttpclientfactory-typed-client.md) | IHttpClientFactory Typed Client | Inject `IBookStackApiClient` directly; never construct `HttpClient` in handlers |
| [ADR-0006](../../architecture/decisions/ADR-0006-systemtextjson-snakecase.md) | System.Text.Json SnakeCaseLower | API client uses `SnakeCaseLower`; do **not** reuse its options in handlers |
| [ADR-0008](../../architecture/decisions/ADR-0008-bookstackapiexception.md) | Typed Exception — BookStackApiException | `IBookStackApiClient` throws `BookStackApiException`; catch it in handlers, not `HttpRequestException` |
| [ADR-0010](../../architecture/decisions/ADR-0010-tool-handler-output-json-policy.md) | Tool Handler JSON Output Naming Policy | Use `JsonNamingPolicy.CamelCase` for all tool and resource output; `private static readonly _jsonOptions` per handler class |

---

## Key Code Patterns

These patterns are established in Task 1 and followed identically in Tasks 2–5.

### Shared `_jsonOptions` field

Every handler class declares this field. No external utility class is used.

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false,
};
```

### Error handling shape

All tool methods use this exact try/catch structure. **Catch `BookStackApiException`** (per
ADR-0008) — not `HttpRequestException`. The spec's code example incorrectly shows
`HttpRequestException`; the actual exception type is `BookStackApiException`.

```csharp
try
{
    // … build request, call _client, serialize result …
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
    _logger.LogError(ex, "BookStack API error: {Message}", ex.Message);
    return JsonSerializer.Serialize(new { error = "api_error", message = ex.ErrorMessage }, _jsonOptions);
}
```

### ID validation

Applied in every tool method that accepts an `id` parameter before any API call.

```csharp
if (id <= 0)
{
    return JsonSerializer.Serialize(
        new { error = "validation_error", message = $"id must be a positive integer, got {id}." },
        _jsonOptions);
}
```

### Export format validation and parsing

Applied in `ExportBookAsync`, `ExportChapterAsync`, `ExportPageAsync`.

```csharp
if (!Enum.TryParse<ExportFormat>(format, ignoreCase: true, out var exportFormat))
{
    return JsonSerializer.Serialize(
        new { error = "validation_error", message = $"Invalid export format '{format}'. Must be one of: html, pdf, plaintext, markdown." },
        _jsonOptions);
}
```

### ListQueryParams construction

Applied in all `ListXxxAsync` methods.

```csharp
var query = (count.HasValue || offset.HasValue || sort is not null)
    ? new ListQueryParams { Count = count, Offset = offset, Sort = sort }
    : null;
var result = await _client.ListXxxAsync(query, ct).ConfigureAwait(false);
```

### Delete success response

All delete tools return this structure.

```csharp
await _client.DeleteXxxAsync(id, ct).ConfigureAwait(false);
return JsonSerializer.Serialize(new { success = true, message = $"Xxx {id} deleted successfully" }, _jsonOptions);
```

### Parameter naming note

C# method parameters use camelCase (`bookId`, `descriptionHtml`, `defaultTemplateId`). The MCP SDK
uses these C# parameter names as JSON schema property names, so MCP clients must send arguments
with the camelCase keys. This differs from the TypeScript reference implementation which uses
snake_case (`book_id`). This is intentional for the .NET implementation.

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

---

### Task 1 — Implement `BookToolHandler`

Replace the six `NotImplementedException` stubs with complete implementations. This task also
establishes the `_jsonOptions` field and error-handling patterns referenced by all subsequent tasks.

**File**: `src/BookStack.Mcp.Server/tools/books/BookToolHandler.cs`

The stubs already store constructor parameters in `_client` and `_logger` backing fields (required
by `TreatWarningsAsErrors`). No change to field declarations.

Add at the top of the class body:

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false,
};
```

Add required usings:

```csharp
using System.Net.Http;
using System.Text.Json;
using BookStack.Mcp.Server.Api.Models;
```

#### `ListBooksAsync` — complete signature and body

```csharp
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
```

#### `ReadBookAsync` — complete signature and body

```csharp
[McpServerTool(Name = "bookstack_books_read")]
[Description("Get a single book by ID, including its full chapter and page hierarchy.")]
public async Task<string> ReadBookAsync(
    [Description("The book ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)
{
    if (id <= 0)
        return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
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
```

#### `CreateBookAsync` — complete signature and body

```csharp
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
```

#### `UpdateBookAsync` — complete signature and body

```csharp
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
        return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
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
```

#### `DeleteBookAsync` — complete signature and body

```csharp
[McpServerTool(Name = "bookstack_books_delete")]
[Description("Delete a book by ID. This moves the book and all its contents to the recycle bin.")]
public async Task<string> DeleteBookAsync(
    [Description("The book ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)
{
    if (id <= 0)
        return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
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
```

#### `ExportBookAsync` — complete signature and body

```csharp
[McpServerTool(Name = "bookstack_books_export")]
[Description("Export a book in the specified format. Returns the raw export content as a string.")]
public async Task<string> ExportBookAsync(
    [Description("The book ID. Must be a positive integer.")] int id,
    [Description("Export format. Must be one of: html, pdf, plaintext, markdown.")] string format,
    CancellationToken ct = default)
{
    if (id <= 0)
        return JsonSerializer.Serialize(new { error = "validation_error", message = $"id must be a positive integer, got {id}." }, _jsonOptions);
    if (!Enum.TryParse<ExportFormat>(format, ignoreCase: true, out var exportFormat))
        return JsonSerializer.Serialize(
            new { error = "validation_error", message = $"Invalid export format '{format}'. Must be one of: html, pdf, plaintext, markdown." },
            _jsonOptions);
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
```

**Acceptance**:

- `dotnet build` succeeds with zero errors and zero warnings.
- No method throws `NotImplementedException`.
- All six methods appear in `tools/list` response with accurate names and non-empty descriptions.

---

### Task 2 — Implement `ChapterToolHandler`

Replace the six stubs in `src/BookStack.Mcp.Server/tools/chapters/ChapterToolHandler.cs`.
Follow the identical `_jsonOptions`, error-handling, and `ConfigureAwait(false)` patterns from
Task 1. Add the same usings.

#### Full method signatures

```csharp
[McpServerTool(Name = "bookstack_chapters_list")]
[Description("List all chapters visible to the authenticated user with pagination options.")]
public async Task<string> ListChaptersAsync(
    [Description("Number of chapters to return (1–500). Defaults to 20.")] int? count = null,
    [Description("Number of chapters to skip for pagination. Defaults to 0.")] int? offset = null,
    [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_chapters_read")]
[Description("Get a single chapter by ID, including its list of pages.")]
public async Task<string> ReadChapterAsync(
    [Description("The chapter ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_chapters_create")]
[Description("Create a new chapter inside a book.")]
public async Task<string> CreateChapterAsync(
    [Description("ID of the book that will contain this chapter. Must be a positive integer.")] int bookId,
    [Description("The chapter name (required, max 255 characters).")] string name,
    [Description("Plain text description (max 1900 characters).")] string? description = null,
    [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
    [Description("Tags to assign. Each object must have name and value string properties.")] IList<Tag>? tags = null,
    CancellationToken ct = default)

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

[McpServerTool(Name = "bookstack_chapters_delete")]
[Description("Delete a chapter by ID. This moves the chapter and all its pages to the recycle bin.")]
public async Task<string> DeleteChapterAsync(
    [Description("The chapter ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_chapters_export")]
[Description("Export a chapter in the specified format. Returns the raw export content as a string.")]
public async Task<string> ExportChapterAsync(
    [Description("The chapter ID. Must be a positive integer.")] int id,
    [Description("Export format. Must be one of: html, pdf, plaintext, markdown.")] string format,
    CancellationToken ct = default)
```

#### Key implementation notes

- `CreateChapterAsync`: validate `bookId > 0` before building `CreateChapterRequest`.
- `UpdateChapterAsync`: validate `id > 0`. If `bookId` is provided, also validate `bookId > 0`.
- `DeleteChapterAsync`: success message is `$"Chapter {id} deleted successfully"`.
- Log message prefix: `"BookStack API error listing/reading/creating/updating/deleting/exporting chapter …"`.

**Acceptance**: All six chapter tools appear in `tools/list`; `dotnet build` passes.

---

### Task 3 — Implement `PageToolHandler`

Replace the six stubs in `src/BookStack.Mcp.Server/tools/pages/PageToolHandler.cs`.
Follow identical patterns from Task 1. The `bookstack_pages_create` method has an additional
validation step.

#### Full method signatures

```csharp
[McpServerTool(Name = "bookstack_pages_list")]
[Description("List all pages visible to the authenticated user with pagination options.")]
public async Task<string> ListPagesAsync(
    [Description("Number of pages to return (1–500). Defaults to 20.")] int? count = null,
    [Description("Number of pages to skip for pagination. Defaults to 0.")] int? offset = null,
    [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_pages_read")]
[Description("Get a single page by ID, including its HTML and Markdown content.")]
public async Task<string> ReadPageAsync(
    [Description("The page ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)

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

[McpServerTool(Name = "bookstack_pages_delete")]
[Description("Delete a page by ID. This moves the page to the recycle bin.")]
public async Task<string> DeletePageAsync(
    [Description("The page ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_pages_export")]
[Description("Export a page in the specified format. Returns the raw export content as a string.")]
public async Task<string> ExportPageAsync(
    [Description("The page ID. Must be a positive integer.")] int id,
    [Description("Export format. Must be one of: html, pdf, plaintext, markdown.")] string format,
    CancellationToken ct = default)
```

#### Key implementation notes

- `CreatePageAsync` — parent validation before API call:

```csharp
if (bookId is null && chapterId is null)
{
    return JsonSerializer.Serialize(
        new { error = "validation_error", message = "Either bookId or chapterId is required." },
        _jsonOptions);
}
```

  If both are provided, pass both to `CreatePageRequest`; the BookStack API resolves precedence.

- `CreatePageRequest` field mapping:

```csharp
var request = new CreatePageRequest
{
    Name = name,
    BookId = bookId,
    ChapterId = chapterId,
    Html = html,
    Markdown = markdown,
    Tags = tags,
};
```

- `DeletePageAsync`: success message is `$"Page {id} deleted successfully"`.

**Acceptance**: All six page tools appear in `tools/list`; `dotnet build` passes.

---

### Task 4 — Implement `ShelfToolHandler`

Replace the five stubs in `src/BookStack.Mcp.Server/tools/shelves/ShelfToolHandler.cs`.
Follow identical patterns from Task 1. There is no export tool for shelves.

#### Full method signatures

```csharp
[McpServerTool(Name = "bookstack_shelves_list")]
[Description("List all bookshelves visible to the authenticated user with pagination options. Shelves group related books into collections.")]
public async Task<string> ListShelvesAsync(
    [Description("Number of shelves to return (1–500). Defaults to 20.")] int? count = null,
    [Description("Number of shelves to skip for pagination. Defaults to 0.")] int? offset = null,
    [Description("Sort field: name, created_at, updated_at. Defaults to name.")] string? sort = null,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_shelves_read")]
[Description("Get a single bookshelf by ID, including the list of books assigned to it.")]
public async Task<string> ReadShelfAsync(
    [Description("The shelf ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)

[McpServerTool(Name = "bookstack_shelves_create")]
[Description("Create a new bookshelf. Optionally assign books to the shelf at creation time.")]
public async Task<string> CreateShelfAsync(
    [Description("The shelf name (required, max 255 characters).")] string name,
    [Description("Plain text description (max 1900 characters).")] string? description = null,
    [Description("HTML description (max 2000 characters). Overrides description if both provided.")] string? descriptionHtml = null,
    [Description("Tags to assign. Each object must have name and value string properties.")] IList<Tag>? tags = null,
    [Description("List of book IDs to assign to this shelf. Providing this array on update replaces ALL currently assigned books.")] IList<int>? books = null,
    CancellationToken ct = default)

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

[McpServerTool(Name = "bookstack_shelves_delete")]
[Description("Delete a bookshelf by ID. The shelf's assigned books are NOT deleted.")]
public async Task<string> DeleteShelfAsync(
    [Description("The shelf ID. Must be a positive integer.")] int id,
    CancellationToken ct = default)
```

#### Key implementation notes

- `CreateShelfAsync` request mapping:

```csharp
var request = new CreateShelfRequest
{
    Name = name,
    Description = description,
    DescriptionHtml = descriptionHtml,
    Tags = tags,
    Books = books,
};
```

- `UpdateShelfAsync` request mapping:

```csharp
var request = new UpdateShelfRequest
{
    Name = name,
    Description = description,
    DescriptionHtml = descriptionHtml,
    Tags = tags,
    Books = books,
};
```

- `DeleteShelfAsync`: success message is `$"Shelf {id} deleted successfully"`.

**Acceptance**: All five shelf tools appear in `tools/list`; `dotnet build` passes.

---

### Task 5 — Implement Resource Handlers

Each handler requires two changes:

1. Replace the `NotImplementedException` in the existing **collection** resource method.
2. Add a new **individual-item** resource method with a `{id}` URI template.

Resource handlers also declare `_jsonOptions` (same definition as tools) and catch
`BookStackApiException` for error handling. Resource methods do not need ID validation because
the URI template binding is handled by the SDK before the method is invoked; an invalid URI simply
will not match the template.

#### `src/BookStack.Mcp.Server/resources/books/BookResourceHandler.cs`

Add `_jsonOptions`, usings, and implement:

```csharp
[McpServerResource(UriTemplate = "bookstack://books", Name = "Books")]
[Description("All books visible to the authenticated user.")]
public async Task<string> GetBooksAsync(CancellationToken ct = default)
{
    var result = await _client.ListBooksAsync(null, ct).ConfigureAwait(false);
    return JsonSerializer.Serialize(result, _jsonOptions);
}

[McpServerResource(UriTemplate = "bookstack://books/{id}", Name = "Book")]
[Description("A single book including its chapter and page hierarchy.")]
public async Task<string> GetBookAsync(
    [Description("The book ID.")] int id,
    CancellationToken ct = default)
{
    var result = await _client.GetBookAsync(id, ct).ConfigureAwait(false);
    return JsonSerializer.Serialize(result, _jsonOptions);
}
```

Both methods wrap in try/catch following the error handling shape from Task 1 (omit 422 catch, as
resource reads do not perform writes). The `GetBooksAsync` catch omits the 404 branch as a list
call does not return 404.

#### `src/BookStack.Mcp.Server/resources/chapters/ChapterResourceHandler.cs`

```csharp
[McpServerResource(UriTemplate = "bookstack://chapters", Name = "Chapters")]
[Description("All chapters visible to the authenticated user.")]
public async Task<string> GetChaptersAsync(CancellationToken ct = default)

[McpServerResource(UriTemplate = "bookstack://chapters/{id}", Name = "Chapter")]
[Description("A single chapter including its list of pages.")]
public async Task<string> GetChapterAsync(
    [Description("The chapter ID.")] int id,
    CancellationToken ct = default)
```

#### `src/BookStack.Mcp.Server/resources/pages/PageResourceHandler.cs`

```csharp
[McpServerResource(UriTemplate = "bookstack://pages", Name = "Pages")]
[Description("All pages visible to the authenticated user.")]
public async Task<string> GetPagesAsync(CancellationToken ct = default)

[McpServerResource(UriTemplate = "bookstack://pages/{id}", Name = "Page")]
[Description("A single page including its HTML and Markdown content.")]
public async Task<string> GetPageAsync(
    [Description("The page ID.")] int id,
    CancellationToken ct = default)
```

#### `src/BookStack.Mcp.Server/resources/shelves/ShelfResourceHandler.cs`

```csharp
[McpServerResource(UriTemplate = "bookstack://shelves", Name = "Shelves")]
[Description("All bookshelves visible to the authenticated user.")]
public async Task<string> GetShelvesAsync(CancellationToken ct = default)

[McpServerResource(UriTemplate = "bookstack://shelves/{id}", Name = "Shelf")]
[Description("A single bookshelf including its list of assigned books.")]
public async Task<string> GetShelfAsync(
    [Description("The shelf ID.")] int id,
    CancellationToken ct = default)
```

**Acceptance**:

- All 8 resources appear in `resources/list` with correct `uriTemplate` values.
- `bookstack://books/{id}` and the three equivalent item templates appear as resource templates
  (distinguishable from static resources in the MCP protocol response).
- `dotnet build` passes.

---

### Task 6 — Tests: `BookToolHandlerTests`

**File**: `tests/BookStack.Mcp.Server.Tests/tools/books/BookToolHandlerTests.cs`

**Namespace**: `BookStack.Mcp.Server.Tests.Tools.Books`

**Setup pattern** (used across all tool handler test files):

```csharp
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Tools.Books;

public sealed class BookToolHandlerTests
{
    private readonly Mock<IBookStackApiClient> _client = new();
    private readonly BookToolHandler _handler;

    public BookToolHandlerTests()
    {
        _handler = new BookToolHandler(_client.Object, NullLogger<BookToolHandler>.Instance);
    }
}
```

#### Test cases (10)

1. **`ListBooksAsync_NoParams_CallsClientWithNullQuery`**
   - Arrange: `_client.Setup(c => c.ListBooksAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new ListResponse<Book>())`
   - Act: call `ListBooksAsync()` with no arguments
   - Assert: `_client.Verify(c => c.ListBooksAsync(null, It.IsAny<CancellationToken>()), Times.Once)`

2. **`ListBooksAsync_WithCountAndOffset_PassesParams`**
   - Arrange: mock returns empty `ListResponse<Book>`
   - Act: call `ListBooksAsync(count: 5, offset: 10)`
   - Assert: verify `ListBooksAsync` called with `ListQueryParams` where `Count == 5` and `Offset == 10`
   - Use `It.Is<ListQueryParams?>(q => q != null && q.Count == 5 && q.Offset == 10)`

3. **`ReadBookAsync_ValidId_ReturnsSerializedBook`**
   - Arrange: mock `GetBookAsync(42, …)` returns a `BookWithContents { Id = 42 }`
   - Act: call `ReadBookAsync(42)`
   - Assert: result is valid JSON; `JsonDocument.Parse(result).RootElement.GetProperty("id").GetInt32()` equals `42`

4. **`ReadBookAsync_NotFound_ReturnsErrorJson`**
   - Arrange: mock throws `new BookStackApiException(404, "Not found", null)`
   - Act: call `ReadBookAsync(999)`
   - Assert: result JSON has `error == "not_found"`; no exception thrown

5. **`CreateBookAsync_ValidName_ReturnsSerializedBook`**
   - Arrange: mock `CreateBookAsync` returns `new Book { Id = 1, Name = "Test" }`
   - Act: call `CreateBookAsync("Test")`
   - Assert: result JSON has `name == "Test"`
   - Also assert: `_client.Verify(c => c.CreateBookAsync(It.Is<CreateBookRequest>(r => r.Name == "Test"), …))`

6. **`CreateBookAsync_WithTags_PassesTagsToRequest`**
   - Arrange: mock returns `new Book { Id = 1, Name = "T" }`
   - Act: call `CreateBookAsync("T", tags: [new Tag { Name = "env", Value = "prod" }])`
   - Assert: verify `CreateBookRequest.Tags` has one element with `Name == "env"`

7. **`UpdateBookAsync_ValidId_ReturnsUpdatedBook`**
   - Arrange: mock `UpdateBookAsync(5, …)` returns `new Book { Id = 5, Name = "Updated" }`
   - Act: call `UpdateBookAsync(5, name: "Updated")`
   - Assert: result JSON has `name == "Updated"`

8. **`DeleteBookAsync_ValidId_ReturnsSuccessJson`**
   - Arrange: mock `DeleteBookAsync(3, …)` returns `Task.CompletedTask`
   - Act: call `DeleteBookAsync(3)`
   - Assert: result JSON has `success == true`; message contains `"3"`

9. **`ExportBookAsync_InvalidFormat_ReturnsValidationError`**
   - Arrange: no mock setup needed (validation fires before API call)
   - Act: call `ExportBookAsync(1, "docx")`
   - Assert: result JSON has `error == "validation_error"`; `_client` not called

10. **`ExportBookAsync_ValidFormatMarkdown_ReturnsExportString`**
    - Arrange: mock `ExportBookAsync(1, ExportFormat.Markdown, …)` returns `"# My Book"`
    - Act: call `ExportBookAsync(1, "markdown")`
    - Assert: result equals `"# My Book"` (raw string, not JSON)

**Acceptance**: `dotnet test` passes all 10 tests; no real HTTP calls made.

---

### Task 7 — Tests: Chapter, Page, and Shelf Tool Handlers

Three test files, one per handler. Follow the same setup pattern from Task 6.

#### `tests/BookStack.Mcp.Server.Tests/tools/chapters/ChapterToolHandlerTests.cs`

Test cases (3):

11. **`CreateChapterAsync_ValidBookIdAndName_ReturnsSerializedChapter`**
    - Arrange: mock returns `new Chapter { Id = 10, BookId = 2, Name = "Ch1" }`
    - Act: call `CreateChapterAsync(bookId: 2, name: "Ch1")`
    - Assert: result JSON has `bookId == 2`; verify `CreateChapterRequest { BookId = 2, Name = "Ch1" }`

12. **`ReadChapterAsync_NotFound_ReturnsErrorJson`**
    - Arrange: mock throws `new BookStackApiException(404, "Not found", null)`
    - Act: call `ReadChapterAsync(999)`
    - Assert: result JSON has `error == "not_found"`; no exception

13. **`DeleteChapterAsync_ValidId_ReturnsSuccessJson`**
    - Arrange: mock `DeleteChapterAsync(7, …)` returns `Task.CompletedTask`
    - Act: call `DeleteChapterAsync(7)`
    - Assert: result JSON has `success == true`; message contains `"7"`

#### `tests/BookStack.Mcp.Server.Tests/tools/pages/PageToolHandlerTests.cs`

Test cases (5):

14. **`CreatePageAsync_NoParentId_ReturnsValidationError`**
    - Arrange: no mock setup
    - Act: call `CreatePageAsync("Page1")` (no `bookId`, no `chapterId`)
    - Assert: result JSON has `error == "validation_error"`; message contains `"bookId"` or `"chapterId"`; client not called

15. **`CreatePageAsync_WithBookId_CallsClientWithCorrectRequest`**
    - Arrange: mock returns `new Page { Id = 5, BookId = 3 }`
    - Act: call `CreatePageAsync("Page1", bookId: 3)`
    - Assert: verify `CreatePageRequest { BookId = 3, Name = "Page1" }`; `ChapterId` is `null`

16. **`CreatePageAsync_WithMarkdown_SetsMarkdownField`**
    - Arrange: mock returns `new Page { Id = 6 }`
    - Act: call `CreatePageAsync("Md Page", chapterId: 1, markdown: "# Hello")`
    - Assert: verify `CreatePageRequest { Markdown = "# Hello", Html = null }`

17. **`ReadPageAsync_ValidId_ReturnsPageWithContent`**
    - Arrange: mock `GetPageAsync(8, …)` returns `new PageWithContent { Id = 8, Html = "<p>Hi</p>" }`
    - Act: call `ReadPageAsync(8)`
    - Assert: result JSON has `html == "<p>Hi</p>"`

18. **`DeletePageAsync_NotFound_ReturnsErrorJson`**
    - Arrange: mock throws `new BookStackApiException(404, "Not found", null)`
    - Act: call `DeletePageAsync(99)`
    - Assert: result JSON has `error == "not_found"`; no exception

#### `tests/BookStack.Mcp.Server.Tests/tools/shelves/ShelfToolHandlerTests.cs`

Test cases (3):

19. **`CreateShelfAsync_WithBooks_SetsBooksList`**
    - Arrange: mock returns `new Bookshelf { Id = 4, Name = "My Shelf" }`
    - Act: call `CreateShelfAsync("My Shelf", books: [1, 2, 3])`
    - Assert: verify `CreateShelfRequest { Books = [1, 2, 3] }`

20. **`UpdateShelfAsync_WithBooks_ReplacesExistingBooks`**
    - Arrange: mock `UpdateShelfAsync(4, …)` returns `new Bookshelf { Id = 4 }`
    - Act: call `UpdateShelfAsync(4, books: [5])`
    - Assert: verify `UpdateShelfRequest { Books = [5] }`

21. **`DeleteShelfAsync_ValidId_ReturnsSuccessJson`**
    - Arrange: mock `DeleteShelfAsync(4, …)` returns `Task.CompletedTask`
    - Act: call `DeleteShelfAsync(4)`
    - Assert: result JSON has `success == true`; message contains `"4"`

**Acceptance**: `dotnet test` passes all 11 tests; no real HTTP calls made.

---

### Task 8 — Tests: Resource Handlers

Two test files covering the four resource handlers. Resource handler tests mock
`IBookStackApiClient` the same way as tool handler tests.

#### `tests/BookStack.Mcp.Server.Tests/resources/books/BookResourceHandlerTests.cs`

Setup:

```csharp
private readonly Mock<IBookStackApiClient> _client = new();
private readonly BookResourceHandler _handler;

public BookResourceHandlerTests()
{
    _handler = new BookResourceHandler(_client.Object, NullLogger<BookResourceHandler>.Instance);
}
```

Test cases (2):

22. **`GetBooksResource_ReturnsSerializedListResponse`**
    - Arrange: mock `ListBooksAsync(null, …)` returns `new ListResponse<Book> { Data = [new Book { Id = 1 }], Total = 1 }`
    - Act: call `GetBooksAsync()`
    - Assert: result is valid JSON with `total == 1`; `data` array has one element

23. **`GetBookByIdResource_ValidId_ReturnsSerializedBookWithContents`**
    - Arrange: mock `GetBookAsync(7, …)` returns `new BookWithContents { Id = 7 }`
    - Act: call `GetBookAsync(7)`
    - Assert: result JSON has `id == 7`

#### `tests/BookStack.Mcp.Server.Tests/resources/pages/PageResourceHandlerTests.cs`

Test cases (2):

24. **`GetPagesResource_ReturnsSerializedListResponse`**
    - Arrange: mock `ListPagesAsync(null, …)` returns `new ListResponse<Page> { Data = [new Page { Id = 3 }], Total = 1 }`
    - Act: call `GetPagesAsync()`
    - Assert: result JSON with `total == 1`

25. **`GetPageByIdResource_ValidId_ReturnsSerializedPageWithContent`**
    - Arrange: mock `GetPageAsync(3, …)` returns `new PageWithContent { Id = 3, Html = "<p>Content</p>" }`
    - Act: call `GetPageAsync(3)`
    - Assert: result JSON has `html == "<p>Content</p>"`

**Acceptance**: `dotnet test` passes all 25 test cases across all 8 test files; no real HTTP calls.

---

## Commands

Executable commands for this project (copy and run directly):

### Build

```
dotnet build src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj --configuration Release
```

### Tests

```
dotnet test tests/BookStack.Mcp.Server.Tests/BookStack.Mcp.Server.Tests.csproj --verbosity normal
```

### Lint / Formatting

```
dotnet format src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj --verify-no-changes
```

### Local Execution (stdio)

```
BOOKSTACK_BASE_URL=http://localhost BOOKSTACK_TOKEN_ID=tid BOOKSTACK_TOKEN_SECRET=sec dotnet run --project src/BookStack.Mcp.Server
```
