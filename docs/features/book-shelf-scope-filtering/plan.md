# Plan: Book/Shelf Scope Filtering (FEAT-0054)

**Feature**: Book/Shelf Scope Filtering
**Spec**: [docs/features/book-shelf-scope-filtering/spec.md](spec.md)
**GitHub Issue**: [#54](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/54)
**Status**: Ready for Implementation
**Date**: 2026-04-25

---

## Referenced ADRs

| ADR | Title | Decision |
|-----|-------|----------|
| [ADR-0014](../../architecture/decisions/ADR-0014-scope-filter-architecture.md) | Scope Filter Architecture | Post-fetch filtering at MCP tool layer; `IOptions<ScopeFilterOptions>`; static `ScopeFilter` helper; indexed config keys |

---

## Data Model Changes

No database tables, EF Core migrations, or `IBookStackApiClient` interface changes. New types are
configuration-layer only.

### New: `ScopeFilterOptions`

```csharp
// src/BookStack.Mcp.Server/config/ScopeFilterOptions.cs
namespace BookStack.Mcp.Server.Config;

public sealed class ScopeFilterOptions
{
    public IReadOnlyList<string> ScopedShelves { get; set; } = [];
    public IReadOnlyList<string> ScopedBooks   { get; set; } = [];

    public bool HasBookScope  => ScopedBooks.Count   > 0;
    public bool HasShelfScope => ScopedShelves.Count > 0;
}
```

### New: `ScopeFilter` (static helper)

```csharp
// src/BookStack.Mcp.Server/config/ScopeFilter.cs
namespace BookStack.Mcp.Server.Config;

internal static class ScopeFilter
{
    internal static bool MatchesScope(int id, string slug, IReadOnlyList<string> scope)
    {
        foreach (var entry in scope)
        {
            if (int.TryParse(entry, out var scopeId) && scopeId == id)
                return true;
            if (string.Equals(entry, slug, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
```

---

## Configuration: Env Var Mapping

`MapBookStackEnvVars()` in `Program.cs` is extended. Each env var value is split on commas,
trimmed, regex-validated (`^[a-zA-Z0-9_-]+$`), and written as indexed `IConfiguration` keys:

```csharp
static void AddScopeEntries(
    Dictionary<string, string?> map,
    string envVar,
    string configPrefix,
    ILogger? logger = null)
{
    var raw = Environment.GetEnvironmentVariable(envVar);
    if (raw is null) return;

    var valid = raw.Split(',')
        .Select(e => e.Trim())
        .Where(e => e.Length > 0)
        .ToList();

    var pattern = new Regex(@"^[a-zA-Z0-9_-]+$");
    var index = 0;
    foreach (var entry in valid)
    {
        if (!pattern.IsMatch(entry))
        {
            // Log warning: entry discarded
            continue;
        }
        map[$"{configPrefix}:{index++}"] = entry;
    }
}
```

Called twice inside `MapBookStackEnvVars()`:

```csharp
AddScopeEntries(map, "BOOKSTACK_SCOPED_BOOKS",   "BookStack:ScopedBooks");
AddScopeEntries(map, "BOOKSTACK_SCOPED_SHELVES", "BookStack:ScopedShelves");
```

`AddBookStackApiClient` registers:

```csharp
services.Configure<ScopeFilterOptions>(configuration.GetSection("BookStack"));
```

---

## Affected Handlers

| Handler | File | Scope | Change |
|---------|------|-------|--------|
| `BookToolHandler` | `tools/books/BookToolHandler.cs` | `ScopedBooks` | `ListBooksAsync` — inject `IOptions<ScopeFilterOptions>`, filter after API call |
| `ShelfToolHandler` | `tools/shelves/ShelfToolHandler.cs` | `ScopedShelves` | `ListShelvesAsync` — inject, filter |
| `SearchToolHandler` | `tools/search/SearchToolHandler.cs` | `ScopedBooks` | `SearchAsync` — inject, filter `SearchResultItem` by `Book.Id/Slug` or item `Id/Slug` when `Type == "book"` |
| `BookResourceHandler` | `resources/books/BookResourceHandler.cs` | `ScopedBooks` | `GetBooksAsync` — inject, filter |
| `ShelfResourceHandler` | `resources/shelves/ShelfResourceHandler.cs` | `ScopedShelves` | `GetShelvesAsync` — inject, filter |

### Filter pattern in `ListBooksAsync` (representative)

```csharp
var result = await _client.ListBooksAsync(query, ct).ConfigureAwait(false);

var scope = _scopeOptions.Value;
if (scope.HasBookScope)
{
    var filtered = result.Data
        .Where(b => ScopeFilter.MatchesScope(b.Id, b.Slug, scope.ScopedBooks))
        .ToList();
    result = new ListResponse<Book>
    {
        Total = filtered.Count,
        From  = result.From,
        To    = result.To,
        Data  = filtered,
    };
}
```

### Filter pattern in `SearchAsync`

```csharp
if (scope.HasBookScope)
{
    var filtered = result.Data.Where(item =>
        item.Type == "book"
            ? ScopeFilter.MatchesScope(item.Id, item.Slug, scope.ScopedBooks)
            : item.Book is not null && ScopeFilter.MatchesScope(
                  item.Book.Id, item.Book.Slug, scope.ScopedBooks))
        .ToList();
    result = new SearchResult
    {
        Total = filtered.Count,
        From  = result.From,
        To    = result.To,
        Data  = filtered,
    };
}
```

---

## VS Code Extension Changes

File: `vscode-extension/package.json` — new entries in `contributes.configuration.properties`:

```json
"bookstack.scopedShelves": {
  "type": "string",
  "default": "",
  "markdownDescription": "Comma-separated shelf IDs or slugs to restrict MCP tools to specific shelves. Leave blank for all shelves."
},
"bookstack.scopedBooks": {
  "type": "string",
  "default": "",
  "markdownDescription": "Comma-separated book IDs or slugs to restrict MCP tools to specific books. Leave blank for all books."
}
```

File: `vscode-extension/src/` — wherever the MCP server spawn env vars are built, add:

```typescript
BOOKSTACK_SCOPED_SHELVES: config.get<string>('bookstack.scopedShelves', ''),
BOOKSTACK_SCOPED_BOOKS:   config.get<string>('bookstack.scopedBooks',   ''),
```

---

## Implementation Phases

### Phase 1 — Config and DI

| Task | File | Description |
|------|------|-------------|
| T1 | `config/ScopeFilterOptions.cs` | Create `ScopeFilterOptions` class |
| T2 | `config/ScopeFilter.cs` | Create static `ScopeFilter` helper |
| T3 | `Program.cs` | Extend `MapBookStackEnvVars()` with scope env var parsing and regex validation |
| T4 | `api/BookStackServiceCollectionExtensions.cs` | Add `services.Configure<ScopeFilterOptions>(configuration.GetSection("BookStack"))` |

### Phase 2 — Tool Handler Updates

| Task | File | Description |
|------|------|-------------|
| T5 | `tools/books/BookToolHandler.cs` | Inject `IOptions<ScopeFilterOptions>`; apply book filter in `ListBooksAsync` |
| T6 | `tools/shelves/ShelfToolHandler.cs` | Inject; apply shelf filter in `ListShelvesAsync` |
| T7 | `tools/search/SearchToolHandler.cs` | Inject; apply book filter to `SearchResult.Data` in `SearchAsync` |

### Phase 3 — Resource Handler Updates

| Task | File | Description |
|------|------|-------------|
| T8 | `resources/books/BookResourceHandler.cs` | Inject; apply book filter in `GetBooksAsync` |
| T9 | `resources/shelves/ShelfResourceHandler.cs` | Inject; apply shelf filter in `GetShelvesAsync` |

### Phase 4 — VS Code Extension

| Task | File | Description |
|------|------|-------------|
| T10 | `vscode-extension/package.json` | Add `bookstack.scopedShelves` and `bookstack.scopedBooks` settings |
| T11 | `vscode-extension/src/<spawn-provider>` | Map settings to `BOOKSTACK_SCOPED_BOOKS` / `BOOKSTACK_SCOPED_SHELVES` env vars |

### Phase 5 — Tests

| Task | Description | Coverage |
|------|-------------|----------|
| T12 | `ScopeFilterTests.cs` | `MatchesScope` with numeric ID, slug (case-insensitive), empty scope, no match |
| T13 | `BookToolHandlerTests.cs` | `ListBooksAsync` scoped/unscoped; `Total` update; read tool unaffected |
| T14 | `ShelfToolHandlerTests.cs` | `ListShelvesAsync` scoped/unscoped |
| T15 | `SearchToolHandlerTests.cs` | `SearchAsync` filters by book; items without book reference excluded |
| T16 | `ProgramTests.cs` / `MapBookStackEnvVarsTests.cs` | Valid entries, whitespace trim, invalid entry discarded with warning |
| T17 | `BookResourceHandlerTests.cs` | `GetBooksAsync` scoped |
| T18 | `ShelfResourceHandlerTests.cs` | `GetShelvesAsync` scoped |

---

## Commands

### Build

```
dotnet build /home/mark/github/bookstack-mcp-server-dotnet/BookStack.Mcp.Server.sln --configuration Release
```

### Tests

```
dotnet test /home/mark/github/bookstack-mcp-server-dotnet/BookStack.Mcp.Server.sln --verbosity normal
```

### Lint / Formatting

```
dotnet format /home/mark/github/bookstack-mcp-server-dotnet/BookStack.Mcp.Server.sln --verify-no-changes
```

### Local Run (stdio)

```
dotnet run --project /home/mark/github/bookstack-mcp-server-dotnet/src/BookStack.Mcp.Server
```

---

## Key Constraints

- No changes to `IBookStackApiClient` or any model class.
- `ListResponse<T>` replacement must set `Total = filtered.Count`; `From`/`To` are left as-is
  (they reflect the raw API pagination offset, not the filtered window).
- `SearchResult` (not `ListResponse`) is the return type of `SearchAsync` — requires a parallel
  filter path.
- Empty scope (`HasBookScope == false` / `HasShelfScope == false`) MUST bypass all filter code —
  verified by CC-03 acceptance criterion.
- Slug comparison: `StringComparison.OrdinalIgnoreCase`.
- Entry validation regex: `^[a-zA-Z0-9_-]+$` — applied in `MapBookStackEnvVars()`, not in
  `ScopeFilter.MatchesScope` (entries in `ScopeFilterOptions` are already validated).
