# Plan: Vector Search Engine and Semantic Search MCP Tool (FEAT-0005)

**Feature**: Vector Search Engine and Semantic Search MCP Tool
**Spec**: [docs/features/vector-search/spec.md](spec.md)
**GitHub Issues**: [#5](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/5),
[#11](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/11)
**Parent Epic**: [#3](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/3)
**Status**: Ready for Implementation
**Date**: 2026-04-26

---

## Referenced ADRs

| ADR | Title | Decision |
|-----|-------|----------|
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution Structure | `IVectorStore` in `Data.Abstractions`; implementations in `Data.Postgres`, `Data.Sqlite`, tests |
| [ADR-0004](../../architecture/decisions/ADR-0004-test-framework.md) | Test Framework | TUnit + Moq; `InMemoryVectorStore` fake in `Tests/Fakes/` |
| [ADR-0009](../../architecture/decisions/ADR-0009-dual-transport-entry-point.md) | Dual-Transport Entry Point | `AddVectorSearch` extension wired in `Program.cs`; `BackgroundService` registered only when `VectorSearch:Enabled = true` |
| [ADR-0010](../../architecture/decisions/ADR-0010-tool-handler-output-json-policy.md) | Tool Handler Output JSON Policy | `bookstack_semantic_search` returns camelCase JSON via `JsonSerializerOptions.CamelCase` |
| [ADR-0015](../../architecture/decisions/ADR-0015-vector-store-abstraction.md) | Vector Store Abstraction | `IVectorStore` custom interface; pgvector, sqlite-vec, in-memory implementations; provider via `VectorSearch:Database` |
| [ADR-0016](../../architecture/decisions/ADR-0016-embedding-provider-abstraction.md) | Embedding Provider Abstraction | `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI`; Ollama default; Azure OpenAI alternative |

---

## New Types

### Configuration Options

```csharp
// src/BookStack.Mcp.Server/config/VectorSearchOptions.cs
namespace BookStack.Mcp.Server.Config;

public sealed class VectorSearchOptions
{
    public bool Enabled { get; set; } = false;
    public string EmbeddingProvider { get; set; } = "Ollama";
    public string Database { get; set; } = "Sqlite";
    public int SyncIntervalHours { get; set; } = 12;
    public OllamaEmbeddingOptions Ollama { get; set; } = new();
    public AzureOpenAIEmbeddingOptions AzureOpenAI { get; set; } = new();
    public VectorSearchDefaults Search { get; set; } = new();
}

public sealed class OllamaEmbeddingOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";
}

public sealed class AzureOpenAIEmbeddingOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class VectorSearchDefaults
{
    public int DefaultTopN { get; set; } = 5;
    public float DefaultMinScore { get; set; } = 0.7f;
}
```

### IVectorStore Abstraction

```csharp
// src/BookStack.Mcp.Server.Data.Abstractions/IVectorStore.cs
namespace BookStack.Mcp.Server.Data.Abstractions;

public interface IVectorStore
{
    Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(ReadOnlyMemory<float> queryVector,
        int topN, float minScore, CancellationToken cancellationToken = default);

    Task DeleteAsync(int pageId, CancellationToken cancellationToken = default);

    Task<string?> GetContentHashAsync(int pageId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default);

    Task SetLastSyncAtAsync(DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);
}
```

### Supporting Types (Data.Abstractions)

```csharp
// VectorPageEntry.cs
public sealed class VectorPageEntry
{
    public int PageId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;       // first 300 chars of plain text
    public DateTimeOffset UpdatedAt { get; init; }
    public string ContentHash { get; init; } = string.Empty;  // SHA-256 of html_content
}

// VectorSearchResult.cs
public sealed class VectorSearchResult
{
    public int PageId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public float Score { get; init; }
}
```

### IBookStackApiClient Extension

Add to `IBookStackApiClient` and implement in `BookStackApiClient.Pages.cs`:

```csharp
// IBookStackApiClient.cs — new method in the Pages section
Task<ListResponse<Page>> GetPagesUpdatedSinceAsync(DateTimeOffset since,
    CancellationToken cancellationToken = default);
```

Implementation uses the BookStack API `filter[updated_at:gte]` query parameter:

```csharp
// BookStackApiClient.Pages.cs
public Task<ListResponse<Page>> GetPagesUpdatedSinceAsync(
    DateTimeOffset since,
    CancellationToken cancellationToken = default)
{
    var ts = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    var url = $"pages?filter[updated_at:gte]={Uri.EscapeDataString(ts)}&count=500";
    return SendAsync<ListResponse<Page>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
}
```

> **Note**: The BookStack API returns up to 500 results per page. For deployments with more than
> 500 pages changed in a single sync window, pagination via `offset` must be implemented in a
> follow-up task. For v1, a single request with `count=500` is acceptable.

---

## File Locations

| Artifact | Path |
|----------|------|
| Configuration options | `src/BookStack.Mcp.Server/config/VectorSearchOptions.cs` |
| `IVectorStore` interface | `src/BookStack.Mcp.Server.Data.Abstractions/IVectorStore.cs` |
| `VectorPageEntry` model | `src/BookStack.Mcp.Server.Data.Abstractions/VectorPageEntry.cs` |
| `VectorSearchResult` model | `src/BookStack.Mcp.Server.Data.Abstractions/VectorSearchResult.cs` |
| pgvector EF entity | `src/BookStack.Mcp.Server.Data.Postgres/VectorPageRecord.cs` |
| `PgVectorStore` | `src/BookStack.Mcp.Server.Data.Postgres/PgVectorStore.cs` |
| pgvector `DbContext` | `src/BookStack.Mcp.Server.Data.Postgres/VectorDbContext.cs` |
| pgvector DI extension | `src/BookStack.Mcp.Server.Data.Postgres/PostgresVectorStoreServiceCollectionExtensions.cs` |
| SQLite EF entity | `src/BookStack.Mcp.Server.Data.Sqlite/VectorPageRecord.cs` |
| `SqliteVecStore` | `src/BookStack.Mcp.Server.Data.Sqlite/SqliteVecStore.cs` |
| SQLite `DbContext` | `src/BookStack.Mcp.Server.Data.Sqlite/VectorDbContext.cs` |
| SQLite DI extension | `src/BookStack.Mcp.Server.Data.Sqlite/SqliteVectorStoreServiceCollectionExtensions.cs` |
| In-memory fake | `tests/BookStack.Mcp.Server.Tests/Fakes/InMemoryVectorStore.cs` |
| Background sync service | `src/BookStack.Mcp.Server/services/VectorIndexSyncService.cs` |
| Tool handler | `src/BookStack.Mcp.Server/tools/semantic-search/SemanticSearchToolHandler.cs` |
| `IBookStackApiClient` extension | `src/BookStack.Mcp.Server/api/IBookStackApiClient.cs` (new method) |
| API client implementation | `src/BookStack.Mcp.Server/api/BookStackApiClient.Pages.cs` (new method) |
| DI wiring extension | `src/BookStack.Mcp.Server/api/VectorSearchServiceCollectionExtensions.cs` |

---

## NuGet Package Changes

### BookStack.Mcp.Server.csproj

Add:

```xml
<PackageReference Include="Microsoft.Extensions.AI" Version="9.*" />
<PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.*" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
```

### BookStack.Mcp.Server.Data.Postgres.csproj

Add:

```xml
<PackageReference Include="Pgvector" Version="0.*" />
```

*(Npgsql.EntityFrameworkCore.PostgreSQL already present — pgvector support is enabled via
`UseVector()` in the `DbContextOptionsBuilder` and `Pgvector` provides the `Vector` CLR type.)*

### BookStack.Mcp.Server.Data.Sqlite.csproj

No additional NuGet packages. The sqlite-vec native library is loaded at runtime via
`NativeLibrary.Load` from the application directory. The binary must be distributed alongside
the application for the SQLite provider to function.

---

## Implementation Phases

### Phase 1 — Abstractions and Configuration

| Task | File | Description |
|------|------|-------------|
| T1 | `config/VectorSearchOptions.cs` | Create `VectorSearchOptions`, `OllamaEmbeddingOptions`, `AzureOpenAIEmbeddingOptions`, `VectorSearchDefaults` |
| T2 | `Data.Abstractions/IVectorStore.cs` | Define `IVectorStore` interface |
| T3 | `Data.Abstractions/VectorPageEntry.cs` | Define `VectorPageEntry` record |
| T4 | `Data.Abstractions/VectorSearchResult.cs` | Define `VectorSearchResult` record |

### Phase 2 — API Client Extension

| Task | File | Description |
|------|------|-------------|
| T5 | `api/IBookStackApiClient.cs` | Add `GetPagesUpdatedSinceAsync(DateTimeOffset since, ct)` |
| T6 | `api/BookStackApiClient.Pages.cs` | Implement using `filter[updated_at:gte]` query parameter |

### Phase 3 — Vector Store Implementations

| Task | File | Description |
|------|------|-------------|
| T7 | `Data.Postgres/VectorPageRecord.cs` | EF Core entity with `Vector` property (Pgvector type), `page_id`, `slug`, `title`, `url`, `excerpt`, `updated_at`, `content_hash` |
| T8 | `Data.Postgres/VectorDbContext.cs` | `DbContext` with `DbSet<VectorPageRecord>` and `SyncState` table; registers pgvector extension with `HasPostgresExtension("vector")` |
| T9 | `Data.Postgres/PgVectorStore.cs` | Implement `IVectorStore`; `SearchAsync` uses `<=>` (cosine distance) via `EF.Functions.CosineDistance`; cosine **similarity** = `1 − distance` |
| T10 | `Data.Postgres/PostgresVectorStoreServiceCollectionExtensions.cs` | `AddPgVectorStore(IServiceCollection, IConfiguration)` extension; registers `IDbContextFactory<VectorDbContext>` and `IVectorStore` |
| T11 | `Data.Sqlite/VectorPageRecord.cs` | EF Core entity; vector stored as `BLOB` (raw `float[]` bytes) for sqlite-vec |
| T12 | `Data.Sqlite/VectorDbContext.cs` | `DbContext`; calls `SqliteVec.Load()` on `Database.Connection` in `OnConfiguring` to activate the sqlite-vec extension |
| T13 | `Data.Sqlite/SqliteVecStore.cs` | Implement `IVectorStore`; `SearchAsync` uses sqlite-vec virtual table query (`vec_distance_cosine`); results filtered to `minScore` in C# if the extension does not support threshold |
| T14 | `Data.Sqlite/SqliteVectorStoreServiceCollectionExtensions.cs` | `AddSqliteVectorStore(IServiceCollection, IConfiguration)` extension |
| T15 | `Tests/Fakes/InMemoryVectorStore.cs` | `IVectorStore` backed by `List<(VectorPageEntry entry, float[] vector)>`; `SearchAsync` computes cosine similarity in C#; thread-safe via `lock` |

### Phase 4 — Background Sync Service

| Task | File | Description |
|------|------|-------------|
| T16 | `services/VectorIndexSyncService.cs` | `BackgroundService` subclass; constructor-injects `IVectorStore`, `IEmbeddingGenerator<string, Embedding<float>>`, `IBookStackApiClient`, `IOptions<VectorSearchOptions>`, `ILogger<VectorIndexSyncService>` |
| | | Loop: `await Task.Delay(interval, ct)` → `GetLastSyncAtAsync` → `GetPagesUpdatedSinceAsync` → per-page SHA-256 → `GetContentHashAsync` → skip or embed → `UpsertAsync` → `SetLastSyncAtAsync` |
| | | Per-page errors caught, logged at `Warning`, loop continues |
| | | `ExecuteAsync` exits cleanly when `CancellationToken` is cancelled (30-second shutdown budget) |

**SHA-256 content hash helper** (inline static method in `VectorIndexSyncService`):

```csharp
private static string ComputeSha256(string content)
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    var hash = System.Security.Cryptography.SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
}
```

**Excerpt extraction** (inline static method — first 300 characters of stripped plain text):

```csharp
private static string ExtractExcerpt(string htmlContent)
{
    // Strip HTML tags; take first 300 characters
    var plain = System.Text.RegularExpressions.Regex.Replace(htmlContent, "<[^>]+>", " ");
    plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();
    return plain.Length <= 300 ? plain : plain[..300];
}
```

### Phase 5 — Semantic Search Tool Handler

| Task | File | Description |
|------|------|-------------|
| T17 | `tools/semantic-search/SemanticSearchToolHandler.cs` | Primary constructor injects `IVectorStore`, `IEmbeddingGenerator<string, Embedding<float>>`, `IOptions<VectorSearchOptions>`, `ILogger<SemanticSearchToolHandler>` |
| | | `bookstack_semantic_search` tool: validate `query` (not null/whitespace → error string), validate `top_n` (1–50 → error string), embed query, call `SearchAsync`, serialise results as JSON array |
| | | Returns `"[]"` when index is empty or no results meet `min_score` |
| | | Returns descriptive string `"Vector search is disabled. Set VectorSearch:Enabled to true to use this tool."` when `!options.Enabled` |

**Tool parameter record**:

```csharp
internal sealed record SemanticSearchInput(
    [property: Required] string Query,
    int TopN = 5,
    float MinScore = 0.7f);
```

### Phase 6 — DI Wiring

| Task | File | Description |
|------|------|-------------|
| T18 | `api/VectorSearchServiceCollectionExtensions.cs` | `AddVectorSearch(IServiceCollection, IConfiguration)` extension method |
| | | Registers `IOptions<VectorSearchOptions>` via `Configure<VectorSearchOptions>(configuration.GetSection("VectorSearch"))` |
| | | If `!options.Enabled`: returns early — no embedding generator, no vector store, no background service registered |
| | | Selects and registers `IEmbeddingGenerator` per `VectorSearch:EmbeddingProvider` |
| | | Selects and delegates to the appropriate `Add*VectorStore` extension per `VectorSearch:Database` |
| | | Registers `VectorIndexSyncService` via `services.AddHostedService<VectorIndexSyncService>()` |
| T19 | `Program.cs` | Add `builder.Services.AddVectorSearch(builder.Configuration)` after existing service registrations |
| T20 | `appsettings.json` | Add `VectorSearch` section with all defaults; `Enabled: false` |

**appsettings.json addition**:

```json
"VectorSearch": {
  "Enabled": false,
  "EmbeddingProvider": "Ollama",
  "Database": "Sqlite",
  "SyncIntervalHours": 12,
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "DeploymentName": "",
    "ApiKey": ""
  },
  "Search": {
    "DefaultTopN": 5,
    "DefaultMinScore": 0.7
  }
}
```

### Phase 7 — Tests

| Task | Test Class | Coverage |
|------|-----------|----------|
| T21 | `VectorIndexSyncServiceTests.cs` | Given unchanged content hash, second sync does not call `GenerateEmbeddingAsync` (Moq `Verify(Times.Never)`) |
| T22 | `VectorIndexSyncServiceTests.cs` | Given new page, sync calls `UpsertAsync` with correct metadata |
| T23 | `VectorIndexSyncServiceTests.cs` | Given per-page embedding failure, cycle continues and remaining pages are processed |
| T24 | `VectorIndexSyncServiceTests.cs` | `SetLastSyncAtAsync` called once per successful cycle |
| T25 | `SemanticSearchToolHandlerTests.cs` | Empty query returns descriptive error string, does not call embedding generator |
| T26 | `SemanticSearchToolHandlerTests.cs` | `top_n` = 0 or 51 returns descriptive error string |
| T27 | `SemanticSearchToolHandlerTests.cs` | Empty vector index returns `"[]"` |
| T28 | `SemanticSearchToolHandlerTests.cs` | `VectorSearch:Enabled = false` returns feature-disabled message |
| T29 | `SemanticSearchToolHandlerTests.cs` | Populated index returns JSON array sorted by descending score |
| T30 | `SemanticSearchToolHandlerTests.cs` | `min_score = 1.0` with sub-threshold results returns `"[]"` |
| T31 | `InMemoryVectorStoreTests.cs` | `UpsertAsync` then `SearchAsync` returns expected result |
| T32 | `InMemoryVectorStoreTests.cs` | `GetContentHashAsync` returns null before upsert, hash after |
| T33 | `BookStackApiClientTests.cs` | `GetPagesUpdatedSinceAsync` sends correct `filter[updated_at:gte]` query parameter |

---

## Key Constraints

- `VectorSearch:Enabled` defaults to `false`. No embedding generator, vector store, or sync
  service is registered when the feature is disabled. The tool handler still registers (via
  assembly scan) but returns the feature-disabled message immediately.
- `IBookStackApiClient.GetPagesUpdatedSinceAsync` uses `count=500` for v1; pagination is deferred.
- SHA-256 is computed over `page.html_content` (the full HTML field from `GetPageAsync`); the
  sync service calls `GetPageAsync` for each updated page to retrieve full content.
- `excerpt` is derived at sync time (first 300 characters of stripped plain text) and stored in
  the vector store; it is not re-derived at query time.
- The tool uses the JSON serialisation policy from ADR-0010 (camelCase, snake_case field aliases
  via `JsonPropertyName` attributes on `VectorSearchResult`).
- Azure OpenAI API keys MUST NOT appear in log messages. The `VectorIndexSyncService` and
  `SemanticSearchToolHandler` log only page IDs, counts, and timing — never configuration values.
- EF Core migrations for `PgVectorStore` and `SqliteVecStore` live in `Data.Postgres` and
  `Data.Sqlite` respectively, not in the main server project (per ADR-0002).

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

### Local Run (stdio, vector search disabled by default)

```
dotnet run --project /home/mark/github/bookstack-mcp-server-dotnet/src/BookStack.Mcp.Server
```

### Local Run (vector search enabled with Ollama)

```
VECTORSEARCH__ENABLED=true \
VECTORSEARCH__EMBEDDINGPROVIDER=Ollama \
VECTORSEARCH__DATABASE=Sqlite \
dotnet run --project /home/mark/github/bookstack-mcp-server-dotnet/src/BookStack.Mcp.Server
```
