# Tasks: Vector Search Engine and Semantic Search MCP Tool (FEAT-0005)

**Feature**: Vector Search Engine and Semantic Search MCP Tool
**Spec**: [docs/features/vector-search/spec.md](spec.md)
**Plan**: [docs/features/vector-search/plan.md](plan.md)
**GitHub Issues**: [#5 — Vector Search Engine](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/5),
[#11 — Semantic Search MCP Tool](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/11)
**Parent Epic**: [#3 — Vector Search](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/3)
**Status**: Ready for Implementation
**Date**: 2026-04-26

---

## Phase 1 — VectorSearchOptions configuration types

**Issue**: [#81](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/81)
**Tracks**: Part of #5

- [ ] Create `src/BookStack.Mcp.Server/config/VectorSearchOptions.cs` with `VectorSearchOptions`, `OllamaEmbeddingOptions`, `AzureOpenAIEmbeddingOptions`, `VectorSearchDefaults`

---

## Phase 2 — IVectorStore abstraction and supporting types

**Issue**: [#82](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/82)
**Tracks**: Part of #5

- [ ] Create `src/BookStack.Mcp.Server.Data.Abstractions/IVectorStore.cs`
- [ ] Create `src/BookStack.Mcp.Server.Data.Abstractions/VectorPageEntry.cs`
- [ ] Create `src/BookStack.Mcp.Server.Data.Abstractions/VectorSearchResult.cs`

---

## Phase 3 — GetPagesUpdatedSinceAsync API client extension

**Issue**: [#83](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/83)
**Tracks**: Part of #5

- [ ] Add `GetPagesUpdatedSinceAsync` to `src/BookStack.Mcp.Server/api/IBookStackApiClient.cs`
- [ ] Implement in `src/BookStack.Mcp.Server/api/BookStackApiClient.Pages.cs`

---

## Phase 4 — InMemoryVectorStore (test fake)

**Issue**: [#84](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/84)
**Tracks**: Part of #5
**Depends on**: #82

- [ ] [P] Create `tests/BookStack.Mcp.Server.Tests/Fakes/InMemoryVectorStore.cs`

---

## Phase 5 — SqliteVectorStore (SK SQLite connector provider)

**Issue**: [#85](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/85)
**Tracks**: Part of #5
**Depends on**: #82

- [ ] [P] Add `Microsoft.SemanticKernel.Connectors.SqliteVec 1.74.0-preview` and `Microsoft.EntityFrameworkCore.Sqlite 10.*` to `BookStack.Mcp.Server.Data.Sqlite.csproj`
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Sqlite/VectorPageRecord.cs` (SK record type with `[VectorStoreRecordKey]` / `[VectorStoreRecordData]` / `[VectorStoreRecordVector]` attributes)
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Sqlite/SyncMetadataDbContext.cs` (EF Core; `sync_metadata` table only)
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Sqlite/SqliteVectorStore.cs` (wraps SK `SqliteVectorStore`; implements `IVectorStore`)
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Sqlite/SqliteVectorStoreServiceCollectionExtensions.cs`

---

## Phase 6 — PgVectorStore (pgvector / PostgreSQL provider)

**Issue**: [#86](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/86)
**Tracks**: Part of #5
**Depends on**: #82

- [ ] [P] Add `Pgvector` NuGet reference to `BookStack.Mcp.Server.Data.Postgres.csproj`
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Postgres/VectorPageRecord.cs`
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Postgres/VectorDbContext.cs`
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Postgres/PgVectorStore.cs`
- [ ] [P] Create `src/BookStack.Mcp.Server.Data.Postgres/PostgresVectorStoreServiceCollectionExtensions.cs`

---

## Phase 7 — VectorIndexSyncService (background sync)

**Issue**: [#87](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/87)
**Tracks**: Part of #5
**Depends on**: #81, #82, #83

- [x] Create `src/BookStack.Mcp.Server/services/VectorIndexSyncService.cs`

---

## Phase 8 — SemanticSearchToolHandler (bookstack_semantic_search tool)

**Issue**: [#88](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/88)
**Tracks**: Part of #11
**Depends on**: #81, #82

- [x] Create `src/BookStack.Mcp.Server/tools/semantic-search/SemanticSearchToolHandler.cs`

---

## Phase 9 — DI wiring, appsettings.json, and NuGet references

**Issue**: [#89](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/89)
**Tracks**: Part of #5
**Depends on**: #81, #82, #85, #86, #87, #88

- [x] Create `src/BookStack.Mcp.Server/api/VectorSearchServiceCollectionExtensions.cs`
- [x] Add `AddVectorSearch` call to `src/BookStack.Mcp.Server/Program.cs`
- [x] Add `VectorSearch` section to `src/BookStack.Mcp.Server/appsettings.json`
- [x] Add `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.Ollama`, `Microsoft.Extensions.AI.OpenAI`, `Azure.AI.OpenAI` to `BookStack.Mcp.Server.csproj`

---

## Phase 10 — TUnit tests

**Issue**: [#90](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/90)
**Tracks**: Part of #5, Part of #11
**Depends on**: #84, #87, #88

- [x] Create `tests/BookStack.Mcp.Server.Tests/VectorSearch/VectorIndexSyncServiceTests.cs`
- [x] Create `tests/BookStack.Mcp.Server.Tests/VectorSearch/SemanticSearchToolHandlerTests.cs`
- [x] Create `tests/BookStack.Mcp.Server.Tests/VectorSearch/VectorSearchIntegrationTests.cs`

---

## Issue Summary

| Phase | Issue | Title | Depends on |
|-------|-------|-------|------------|
| 1 | [#81](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/81) | VectorSearchOptions configuration types | — |
| 2 | [#82](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/82) | IVectorStore abstraction and supporting types | — |
| 3 | [#83](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/83) | GetPagesUpdatedSinceAsync API client extension | — |
| 4 | [#84](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/84) | InMemoryVectorStore (test fake) | #82 |
| 5 | [#85](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/85) | SqliteVectorStore (SK SQLite connector provider) | #82 |
| 6 | [#86](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/86) | PgVectorStore (pgvector / PostgreSQL provider) | #82 |
| 7 | [#87](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/87) | VectorIndexSyncService (background sync) | #81, #82, #83 |
| 8 | [#88](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/88) | SemanticSearchToolHandler | #81, #82 |
| 9 | [#89](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/89) | DI wiring, appsettings.json, NuGet refs | #81, #82, #85, #86, #87, #88 |
| 10 | [#90](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/90) | TUnit tests | #84, #87, #88 |
