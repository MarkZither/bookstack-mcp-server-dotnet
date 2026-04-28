# ADR-0015: Vector Store Abstraction and Provider Selection

**Status**: Accepted
**Date**: 2026-04-26
**Updated**: 2026-04-27
**Author**: GitHub Copilot
**Deciders**: Engineering team

---

## Context

The Vector Search feature (FEAT-0005) requires persistent storage for page embedding vectors,
per-page content hashes (for change detection), and the last-sync timestamp (for incremental
indexing). Three deployment scenarios must be served:

1. **Production — PostgreSQL** (`BookStack.Mcp.Server.Data.Postgres`): users already running
   BookStack on PostgreSQL can enable pgvector with a single EF Core migration.
2. **Team / enterprise — SQL Server** (`BookStack.Mcp.Server.Data.SqlServer`): SQL Server 2025
   ships a native `VECTOR` datatype; organisations on SQL Server can use the same EF Core
   migration pattern without a separate vector extension.
3. **Lightweight / developer — SQLite** (`BookStack.Mcp.Server.Data.Sqlite`): local development
   and single-user deployments that do not require a separate database service.
4. **Unit tests**: no database process; tests must run in CI without any external service.

ADR-0002 established the `Data.Abstractions` / per-provider project split specifically to
support this pattern. This approach mirrors the data layer of
[DeepWikiOpenDotnet](https://github.com/MarkZither/DeepWikiOpenDotnet/tree/main/src), where
`Data.Abstractions` defines the interface and entity models, per-provider projects supply
EF Core `DbContext`, entity configuration, and EF migrations, and the consuming service
depends only on the abstraction.

The abstraction must expose at minimum: upsert, cosine-similarity search (top-N, min-score
threshold), delete-by-page-id, content-hash lookup, and last-sync-timestamp get/set. It must
be mockable in unit tests without running a database.

## Decision

We will define a custom `IVectorStore` interface in `BookStack.Mcp.Server.Data.Abstractions`
with four implementations selected at startup via the `VectorSearch:Database` configuration
key (`"Postgres"` | `"SqlServer"` | `"Sqlite"` | `"InMemory"`).

**Interface contract** (`IVectorStore`):

```
UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, ct)
SearchAsync(ReadOnlyMemory<float> queryVector, int topN, float minScore, ct)
  → IReadOnlyList<VectorSearchResult>
DeleteAsync(int pageId, ct)
GetContentHashAsync(int pageId, ct) → string?
GetLastSyncAtAsync(ct) → DateTimeOffset?
SetLastSyncAtAsync(DateTimeOffset timestamp, ct)
```

**Supporting types** (both in `Data.Abstractions`):

- `VectorPageEntry` — carries `PageId`, `Slug`, `Title`, `Url`, `Excerpt`, `UpdatedAt`,
  `ContentHash` for upsert and metadata storage.
- `VectorSearchResult` — read model returned by `SearchAsync` with `PageId`, `Title`, `Url`,
  `Excerpt`, `Score`.

**Implementations**:

| Key | Class | Project | Engine |
|-----|-------|---------|--------|
| `"Postgres"` | `PgVectorStore` | `Data.Postgres` | Npgsql.EntityFrameworkCore.PostgreSQL + Pgvector.EntityFrameworkCore; cosine similarity via `<=>` operator; HNSW index |
| `"SqlServer"` | `SqlServerVectorStore` | `Data.SqlServer` | Microsoft.EntityFrameworkCore.SqlServer targeting SQL Server 2025 native `VECTOR` type; cosine similarity via `VECTOR_DISTANCE('cosine', ...)` |
| `"Sqlite"` | `SqliteVectorStore` | `Data.Sqlite` | `Microsoft.SemanticKernel.Connectors.Sqlite` (SK SQLite connector); native SQLite vector support via the SK `IVectorStoreRecordCollection<K,V>` API; sync metadata in a separate `sync_metadata` table via EF Core Sqlite |
| `"InMemory"` | `InMemoryVectorStore` | `Tests/Fakes` | `List<(VectorPageEntry, float[])>` with cosine similarity computed in C# |

The active implementation is registered as `Singleton<IVectorStore>` in DI. For Postgres and
SQL Server the corresponding `DbContext` is registered via the provider's `IServiceCollection`
extension method; schema creation and updates are handled by **EF Core migrations** applied at
startup via `dbContext.Database.MigrateAsync()`. For SQLite, the SK connector manages its own
table schema; a minimal EF Core `SyncMetadataDbContext` handles the `sync_metadata` table
(content hash, last-sync timestamp) and its migration. The `InMemory` implementation has no
`DbContext` and requires no migration.

**NuGet packages** to add/pin in provider projects:

| Project | Package | Version |
|---------|---------|---------|
| `Data.Postgres` | `Npgsql` | `10.0.2` |
| `Data.Postgres` | `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.0` |
| `Data.Postgres` | `Pgvector` | `0.3.2` |
| `Data.Postgres` | `Pgvector.EntityFrameworkCore` | `0.3.0` |
| `Data.SqlServer` | `Microsoft.EntityFrameworkCore.SqlServer` | `10.*` (already referenced) |
| `Data.Sqlite` | `Microsoft.SemanticKernel.Connectors.SqliteVec` | `1.74.0-preview` |
| `Data.Sqlite` | `Microsoft.EntityFrameworkCore.Sqlite` | `10.*` (sync metadata table only) |

## Rationale

### EF Core + migrations, not a hand-rolled schema

EF Core migrations provide a reproducible, version-controlled schema history. The same `dotnet
ef migrations add` / `dotnet ef database update` workflow applies to all three database
providers, and `MigrateAsync()` at startup means deployments are self-updating without any
out-of-band DBA step. This matches the DeepWikiOpenDotnet approach that has already proven the
pattern in production.

### IVectorStore as custom interface, not Microsoft.Extensions.VectorData.Abstractions

`Microsoft.Extensions.VectorData.Abstractions` (the Microsoft vector data abstraction) is in
preview as of April 2026 and its API surface is still evolving for .NET 10. The pgvector and
EF Core vector providers under that namespace have not reached stable status. A purpose-built
`IVectorStore` with a minimal six-method surface gives full control and zero churn risk; it
can be migrated to the Microsoft abstraction in a future ADR once it reaches GA.

### PostgreSQL — pgvector via Npgsql

Npgsql 10.0.0 and Pgvector.EntityFrameworkCore 0.3.0 are the current stable releases that
target .NET 10 / EF Core 10. The `<=>` cosine distance operator is pushed server-side; an HNSW
index (configured via entity configuration fluent API) provides sub-linear ANN query performance
at indexing volumes typical for a BookStack instance (10k–100k pages).

### SQL Server 2025 — native VECTOR type

SQL Server 2025 introduces a first-class `VECTOR(n)` column type and the
`VECTOR_DISTANCE('cosine', col, @query)` function. This eliminates any third-party extension;
the Microsoft.EntityFrameworkCore.SqlServer provider maps the column natively once the
`UseVectorSearch()` option is enabled. Targeting SQL Server 2025 is a deliberate choice — users
on earlier versions must use Postgres or SQLite.

### SQLite — Semantic Kernel SQLite connector

`Microsoft.SemanticKernel.Connectors.SqliteVec` (the renamed successor to the deprecated
`Microsoft.SemanticKernel.Connectors.Sqlite`) provides native SQLite vector support via the SK
`IVectorStoreRecordCollection<K,V>` API. It handles schema creation, vector serialisation, and
cosine similarity computation without requiring a separate native binary. The SK connector is
currently preview (`1.74.0-preview`); if a breaking change occurs, the impact is confined to
`SqliteVectorStore` in `Data.Sqlite`. Sync metadata (content hash, last-sync timestamp) lives
in a separate `sync_metadata` table managed by a minimal EF Core `SyncMetadataDbContext` with
its own migration. The SQLite provider is intended for local/developer deployments; Postgres or
SQL Server should be used for team or production workloads.

### Singleton lifetime

Vector store implementations are stateless DB clients backed by connection pools managed by EF
Core. Registering as `Singleton` matches the `IDbContextFactory<T>` pattern used by the
long-lived `VectorIndexSyncService`.

## Alternatives Considered

### Option A: Use Microsoft.Extensions.VectorData.Abstractions

- **Pros**: Microsoft-supported; ecosystem alignment.
- **Cons**: Preview API with breaking changes in .NET 10 RC cycle; EF Core vector providers
  are not stable; tight coupling to a library the team does not control.
- **Why rejected**: API instability risk; custom interface is simpler and already proven in
  DeepWikiOpenDotnet.

### Option B: Qdrant / Weaviate / Chroma via their .NET clients

- **Pros**: Purpose-built vector databases; rich query capabilities.
- **Cons**: Requires an additional external service; contradicts the project's goal of a
  self-contained local tool.
- **Why rejected**: Operational overhead not justified for a local MCP server scenario.

### Option C: sqlite-vec native extension for SQLite

- **Pros**: Potentially faster ANN queries via a purpose-built native vector index.
- **Cons**: Requires distributing a native binary per platform; `NativeLibrary.Load` at startup
  adds deployment complexity; the SK SQLite connector achieves the same result without any
  native dependency.
- **Why rejected**: Deployment complexity outweighs the benefit when `Microsoft.SemanticKernel
  .Connectors.Sqlite` covers the same use case with a pure managed package.

## Consequences

### Positive

- The main server (`BookStack.Mcp.Server`) depends only on `Data.Abstractions` — consistent with
  ADR-0002; no DB driver leaks into the server assembly.
- Providers swap via a single configuration key; no code change required.
- EF Core migrations are version-controlled and self-applied at startup.
- `InMemoryVectorStore` in the test project eliminates all database dependencies from unit tests.
- SQL Server 2025 support means enterprise users on SQL Server have a first-class path without
  requiring Postgres.

### Negative / Trade-offs

- SQLite vector search is O(n) — not suitable for large instances. Documentation must clearly
  recommend Postgres or SQL Server for team use.
- SQL Server 2025 is required for the SQL Server provider; older versions are not supported.
- `IVectorStore` is a custom abstraction that must be maintained if requirements change.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
- [ADR-0004: Test Framework](ADR-0004-test-framework.md)
- [ADR-0016: Embedding Provider Abstraction](ADR-0016-embedding-provider-abstraction.md)


The Vector Search feature (FEAT-0005) requires persistent storage for page embedding vectors,
per-page content hashes (for change detection), and the last-sync timestamp (for incremental
indexing). Three deployment scenarios must be served:

1. **Production — PostgreSQL** (`BookStack.Mcp.Server.Data.Postgres`): users already running
   BookStack on PostgreSQL can enable pgvector with a single migration.
2. **Lightweight / developer — SQLite** (`BookStack.Mcp.Server.Data.Sqlite`): local development
   and single-user deployments that do not require a separate database service.
3. **Unit tests**: no database process; tests must run in CI without any external service.

ADR-0002 established the `Data.Abstractions` / per-provider project split specifically to
support this pattern. The SQL Server provider (`Data.SqlServer`) is explicitly out of scope
for FEAT-0005 (no stable EF Core SQL Server vector extension at the time of writing).

The abstraction must expose at minimum: upsert, cosine-similarity search (top-N, min-score
threshold), delete-by-page-id, content-hash lookup, and last-sync-timestamp get/set. It must
be mockable in unit tests without running a database.

## Decision

We will define a custom `IVectorStore` interface in `BookStack.Mcp.Server.Data.Abstractions`
with three implementations selected at startup via the `VectorSearch:Database` configuration
key (`"Postgres"` | `"Sqlite"` | `"InMemory"`).

**Interface contract** (`IVectorStore`):

```
UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, ct)
SearchAsync(ReadOnlyMemory<float> queryVector, int topN, float minScore, ct)
  → IReadOnlyList<VectorSearchResult>
DeleteAsync(int pageId, ct)
GetContentHashAsync(int pageId, ct) → string?
GetLastSyncAtAsync(ct) → DateTimeOffset?
SetLastSyncAtAsync(DateTimeOffset timestamp, ct)
```

**Supporting types** (both in `Data.Abstractions`):

- `VectorPageEntry` — carries `PageId`, `Slug`, `Title`, `Url`, `Excerpt`, `UpdatedAt`,
  `ContentHash` for upsert and metadata storage.
- `VectorSearchResult` — read model returned by `SearchAsync` with `PageId`, `Title`, `Url`,
  `Excerpt`, `Score`.

**Implementations**:

| Key | Class | Project | Engine |
|-----|-------|---------|--------|
| `"Postgres"` | `PgVectorStore` | `Data.Postgres` | Npgsql + pgvector extension; cosine similarity via `<=>` operator |
| `"Sqlite"` | `SqliteVecStore` | `Data.Sqlite` | `Microsoft.EntityFrameworkCore.Sqlite` + sqlite-vec native extension loaded at startup |
| `"InMemory"` | `InMemoryVectorStore` | `Tests/Fakes` | `List<(VectorPageEntry, float[])>` with cosine similarity computed in C# |

The active implementation is registered as `Singleton<IVectorStore>` in DI. For Postgres and
SQLite, the corresponding `DbContext` is also registered by the provider's
`IServiceCollection` extension method. The `InMemory` implementation has no `DbContext`.

New NuGet references required:

| Project | Package | Version |
|---------|---------|---------|
| `Data.Postgres` | `Pgvector` | `0.*` |
| `Data.Sqlite` | `sqlite-vec` (native lib via `NativeLibrary.Load`) | n/a — loaded at runtime |

## Rationale

### IVectorStore as custom interface, not Microsoft.Extensions.VectorData.Abstractions

`Microsoft.Extensions.VectorData.Abstractions` (the Microsoft vector data abstraction) is in
preview as of April 2026 and its API surface is still evolving for .NET 10. The pgvector and
sqlite-vec providers under that namespace have not reached stable status. Binding to it now
risks breaking changes before the feature stabilises. A purpose-built `IVectorStore` with a
minimal six-method surface gives full control and zero churn risk; it can be migrated to the
Microsoft abstraction in a future ADR once it reaches GA.

### Singleton lifetime

Vector store implementations are stateless DB clients backed by connection pools managed by EF
Core. Registering as `Singleton` matches EF Core `DbContext` factory pattern (`IDbContextFactory`)
used by the long-lived `VectorIndexSyncService`.

### Cosine similarity delegation

pgvector and sqlite-vec both compute cosine similarity natively in the database, enabling
server-side top-N filtering without loading all vectors into process memory.
`InMemoryVectorStore` computes it in C# using the dot-product / magnitude formula; this is
acceptable because the in-memory store is only used in unit tests with a small number of entries.

## Alternatives Considered

### Option A: Use Microsoft.Extensions.VectorData.Abstractions

- **Pros**: Microsoft-supported; ecosystem alignment; potential future NuGet provider packages.
- **Cons**: Preview API with breaking changes in .NET 10 RC cycle; pgvector and sqlite-vec
  providers are not stable; tight coupling to a library the team does not control.
- **Why rejected**: API instability risk is unacceptable for a feature targeting a stable release.

### Option B: Qdrant / Weaviate / Chroma via their .NET clients

- **Pros**: Purpose-built vector databases; rich query capabilities; cloud-native options.
- **Cons**: Requires an additional external service; contradicts the project's goal of a
  self-contained local tool; adds deployment complexity for users who only want local semantic
  search.
- **Why rejected**: Operational overhead is not justified for a local MCP server scenario.

### Option C: Define IVectorStore inside the main server project

- **Pros**: Slightly simpler — no cross-project reference needed for the in-memory test fake.
- **Cons**: Violates ADR-0002's `Data.Abstractions` boundary; `Data.Postgres` and `Data.Sqlite`
  cannot implement an interface they cannot reference; forces test fakes to import the main
  server project.
- **Why rejected**: Contradicts the architectural separation established in ADR-0002.

## Consequences

### Positive

- The main server (`BookStack.Mcp.Server`) depends only on `Data.Abstractions` — consistent with
  ADR-0002; no DB driver leaks into the server assembly.
- Providers swap via a single configuration key; no code change required.
- `InMemoryVectorStore` in the test project eliminates all database dependencies from unit tests.
- The six-method interface is small enough to mock with Moq in a single `Setup` call per test.

### Negative / Trade-offs

- `IVectorStore` is a custom abstraction that must be maintained if requirements change (e.g.,
  hybrid filtering, metadata-only queries).
- The cosine similarity implementation in `InMemoryVectorStore` diverges from the SQL
  implementations; tests must not rely on numerical precision parity between stores.
- sqlite-vec is loaded via `NativeLibrary.Load` at startup; the native binary must be distributed
  with the application for the SQLite provider to function.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
- [ADR-0004: Test Framework](ADR-0004-test-framework.md)
- [ADR-0016: Embedding Provider Abstraction](ADR-0016-embedding-provider-abstraction.md)
