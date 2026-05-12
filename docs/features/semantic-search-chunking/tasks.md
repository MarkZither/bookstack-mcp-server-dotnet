# Tasks: Semantic Search Quality — Golden-Dataset Evaluation and Chunking Strategy (FEAT-0060)

**Feature**: Semantic Search Quality — Golden-Dataset Evaluation and Chunking Strategy
**Spec**: [docs/features/semantic-search-chunking/spec.md](spec.md)
**GitHub Issues**: [#100 (epic)](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/100), [#101](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/101)–[#110](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/110)
**Parent Epic**: [#3 — Vector Search](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/3)
**Status**: Ready for Implementation
**Date**: 2026-05-12

---

## Missing ADRs (blocking Phase 2 work)

Two architectural decisions are referenced in the spec but not yet formalized as ADRs.
These are created as blocking tasks in Phase 5/6 (conditional on Phase 1 metrics).

| Decision | Scope | Blocks |
|----------|-------|--------|
| Composite PK migration strategy — `(page_id, chunk_index)` across Postgres + SQLite | Cross-cutting | Phase 11 |
| NuGet package repo & versioning — `MarkZither/rag-chunking-dotnet`, multi-target, signing | Cross-cutting | Phase 7 |

---

## Phase 1 — Extend Seed-BookStack.ps1 with golden-dataset content

**Issue**: [#101](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/101)
**Tracks**: Part of #3
**Refs**: Req 1, 2

- [x] Add at least 3 WYSIWYG-editor pages (`Editor = "wysiwyg"`) to `scripts/Seed-BookStack.ps1`
- [x] Add at least 3 Markdown-editor pages (`Editor = "markdown"`) to `scripts/Seed-BookStack.ps1`
- [x] Add at least 1 page containing a DrawIO diagram block to `scripts/Seed-BookStack.ps1`
- [x] Create `tests/BookStack.Mcp.Server.Tests/Evaluation/golden-dataset.json` with ≥ 20 `{ query, expected_page_slug }` pairs covering short, long, code-heavy, and multi-topic pages
- [x] Document the DrawIO HTML structure observed in the raw `Html` API response (comment in seed script or in `docs/features/semantic-search-chunking/drawio-html-notes.md`)

---

## Phase 2 — Scaffold evaluation harness project

**Issue**: [#102](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/102)
**Tracks**: Part of #3
**Depends on**: Phase 1

- [ ] Create `tests/BookStack.Mcp.Server.Evaluation/BookStack.Mcp.Server.Evaluation.csproj` (TUnit test project or .NET console, referencing `golden-dataset.json` as embedded resource)
- [ ] Add project to `BookStack.Mcp.Server.sln`
- [ ] Create `tests/BookStack.Mcp.Server.Evaluation/EvaluationHarness.cs` skeleton with configuration loading (`BookStackBaseUrl`, `ApiToken`, `VectorSearch:*`) from environment variables / `appsettings.Evaluation.json`
- [ ] Verify `dotnet run` / `dotnet test` executes against a dev BookStack instance without code changes (AC from spec)

---

## Phase 3 — Implement evaluation metrics (Recall@K, MRR, score histogram)

**Issue**: [#103](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/103)
**Tracks**: Part of #3
**Depends on**: Phase 2

- [ ] Implement `EvaluationHarness.TriggerFullSyncAndWaitAsync()` — calls `VectorIndexSyncService` via MCP tool or direct DI and polls until sync completes
- [ ] Implement `EvaluationHarness.RunQueriesAsync()` — calls `bookstack_semantic_search` for each golden-dataset query, records ranked result lists
- [ ] Implement `MetricsCalculator.ComputeRecallAtK(results, k)` → `float`
- [ ] Implement `MetricsCalculator.ComputeMRR(results)` → `float`
- [ ] Implement `MetricsCalculator.ComputeScoreHistogram(results)` — buckets for correct vs. incorrect hits
- [ ] Add unit tests for `MetricsCalculator` using synthetic result fixtures in `tests/BookStack.Mcp.Server.Tests/Evaluation/MetricsCalculatorTests.cs`

---

## Phase 4 — Markdown report output and quality-gate review

**Issue**: [#104](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/104)
**Tracks**: Part of #3
**Depends on**: Phase 3

- [ ] Implement `ReportGenerator.GenerateMarkdownReport(EvaluationResult)` → `string` in `tests/BookStack.Mcp.Server.Evaluation/ReportGenerator.cs`
- [ ] Report MUST include: `Recall@1`, `Recall@3`, `MRR`, score histogram, pass/fail verdict per metric, and overall gate decision (`Phase 2 required / investigate / not required`)
- [ ] Wire up full harness: seed → sync → query → metrics → report written to `docs/features/semantic-search-chunking/evaluation-report.md`
- [ ] Run harness against seeded dev instance, commit `evaluation-report.md` to the PR
- [ ] Update spec status to `Implemented` if all metrics pass; create Phase 5–14 issues if any metric fails/investigates

---

## Phase 5 — ADR-0020: Composite PK migration strategy (conditional — blocks Phase 11)

**Issue**: [#105](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/105)
**Tracks**: Part of #3 (conditional on Phase 4 gate)
**Depends on**: Phase 4 gate decision

- [ ] Write `docs/architecture/decisions/ADR-0020-composite-pk-chunk-index.md` covering: rationale for `(page_id, chunk_index)` composite PK, Postgres + SQLite EF Core migration approach, backward compatibility for existing single-chunk rows (`ChunkIndex = 0, TotalChunks = 1`), and rollback strategy
- [ ] Get ADR accepted before any Phase 11 migration code is written

---

## Phase 6 — ADR-0021: NuGet package repo and versioning strategy (conditional — blocks Phase 7)

**Issue**: [#106](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/106)
**Tracks**: Part of #3 (conditional on Phase 4 gate)
**Depends on**: Phase 4 gate decision

- [ ] Write `docs/architecture/decisions/ADR-0021-rag-chunking-nuget-package.md` covering: new repo `MarkZither/rag-chunking-dotnet`, multi-target `net9.0`/`net10.0`, NuGet package signing (Certum or GitHub Actions sigstore), versioning (SemVer, initial `1.0.0`), CI/CD for publish to NuGet.org, and relationship to `DeepWikiOpenDotnet`
- [ ] Get ADR accepted before Phase 7 work starts

---

## Phase 7 — Scaffold `MarkZither.Rag.Chunking` project (conditional)

**Issue**: [#107](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/107)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 6 (ADR-0021)

- [ ] Create new repository `MarkZither/rag-chunking-dotnet` with standard structure
- [ ] Create `src/MarkZither.Rag.Chunking/MarkZither.Rag.Chunking.csproj` targeting `net9.0;net10.0`
- [ ] Create `tests/MarkZither.Rag.Chunking.Tests/MarkZither.Rag.Chunking.Tests.csproj` (TUnit)
- [ ] Add `Tiktoken` NuGet reference (`v2.0.3`) and verify license compatibility
- [ ] Add GitHub Actions CI workflow (build + test on push/PR)
- [ ] Add NuGet publish workflow (on release tag)

---

## Phase 8 — Implement core interfaces, types, and `SlideWindowChunkingService` (conditional)

**Issue**: [#107](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/107)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 7

- [ ] [P] Create `src/MarkZither.Rag.Chunking/ITokenEncoder.cs` (`CountTokens(string text) → int`)
- [ ] [P] Create `src/MarkZither.Rag.Chunking/TiktokenEncoder.cs` (cl100k_base, implements `ITokenEncoder`)
- [ ] [P] Create `src/MarkZither.Rag.Chunking/ChunkOptions.cs` (`ChunkSize=512`, `ChunkOverlap=128`, `MaxChunksPerDocument=200`, `StripHtml=true`)
- [ ] [P] Create `src/MarkZither.Rag.Chunking/TextChunk.cs` (record: `Text`, `ChunkIndex`, `TotalChunks`, `TokenCount`)
- [ ] [P] Create `src/MarkZither.Rag.Chunking/IChunkingService.cs` (`ChunkAsync(string, ChunkOptions, CancellationToken) → IReadOnlyList<TextChunk>`)
- [ ] Create `src/MarkZither.Rag.Chunking/Internal/HtmlStripper.cs` (removes `<script>`, `<style>`, tags; strips DrawIO `<div>`/`<figure>` blocks containing base64 XML; configurable DrawIO regex; no `HtmlAgilityPack` dependency)
- [ ] Create `src/MarkZither.Rag.Chunking/SlideWindowChunkingService.cs` — token-aware sliding window with overlap, sentence/paragraph boundary snapping; mirrors `DeepWiki.Rag.Core.Tokenization.Chunker` (commit `8b5887b`); enforces `MaxChunksPerDocument` and 5 MB input guard
- [ ] Create `src/MarkZither.Rag.Chunking/ServiceCollectionExtensions.cs` (`AddChunking(IServiceCollection)`)
- [ ] Validate algorithm parity with `DeepWiki.Rag.Core.Tokenization.Chunker` (commit `8b5887b`) — document any intentional deviations

---

## Phase 9 — TUnit tests for `MarkZither.Rag.Chunking` and NuGet publish (conditional)

**Issue**: [#107](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/107)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 8

- [ ] Write tests for `SlideWindowChunkingService`: short text (< `ChunkSize`), exact-fit, multi-chunk, overlap correctness, `MaxChunksPerDocument` ceiling, `ChunkSize=0` returns full text, boundary snapping
- [ ] Write tests for `HtmlStripper`: tag removal, `<script>`/`<style>` removal, DrawIO block detection + removal, no-op on clean text, regex-configurable DrawIO pattern
- [ ] Write tests for `TiktokenEncoder`: known token counts for fixed strings
- [ ] Achieve > 90% line coverage (verified by `dotnet test --collect:"XPlat Code Coverage"`)
- [ ] Configure NuGet package metadata (`PackageId`, `Authors`, `Description`, `RepositoryUrl`, `PackageLicenseExpression`)
- [ ] Enable NuGet package signing per ADR-0021
- [ ] Tag `v1.0.0` and verify the publish workflow pushes to NuGet.org staging feed

---

## Phase 10 — `IVectorStore` and `Data.Abstractions` type changes (conditional)

**Issue**: [#108](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/108)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 5 (ADR-0020), Phase 9

- [ ] Add `DeleteChunksAsync(int pageId, CancellationToken ct)` to `src/BookStack.Mcp.Server.Data.Abstractions/IVectorStore.cs`
- [ ] Add `ChunkIndex` (int, default `0`) and `TotalChunks` (int, default `1`) to `src/BookStack.Mcp.Server.Data.Abstractions/VectorPageEntry.cs`
- [ ] Add `ChunkIndex` (int) and `Excerpt` (string) to `src/BookStack.Mcp.Server.Data.Abstractions/VectorSearchResult.cs`
- [ ] Add `MarkZither.Rag.Chunking` NuGet reference and `Chunking` property (type `ChunkOptions`) to `src/BookStack.Mcp.Server/config/VectorSearchOptions.cs`
- [ ] Update `InMemoryVectorStore` test fake to implement `DeleteChunksAsync` and composite PK semantics
- [ ] Update `src/BookStack.Mcp.Server.Data.Abstractions/VectorSearchResult.cs` XML doc to note Option A deduplication contract

---

## Phase 11 — Postgres and SQLite schema migrations (conditional)

**Issue**: [#108](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/108)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 5 (ADR-0020), Phase 10

- [ ] [P] Add `chunk_index` (int, not null, default 0) and `total_chunks` (int, not null, default 1) columns to the Postgres `vector_page_entries` table via EF Core migration in `BookStack.Mcp.Server.Data.Postgres`
- [ ] [P] Change Postgres PK from `page_id` to `(page_id, chunk_index)` composite in the same migration; verify existing rows are backward compatible (`chunk_index = 0`)
- [ ] [P] Implement `PgVectorStore.DeleteChunksAsync` — `DELETE FROM vector_page_entries WHERE page_id = $1`
- [ ] [P] Add `chunk_index` and `total_chunks` columns to the SQLite `vector_page_entries` table via EF Core migration in `BookStack.Mcp.Server.Data.Sqlite`
- [ ] [P] Change SQLite PK to `(page_id, chunk_index)` composite in the same migration
- [ ] [P] Implement `SqliteVecStore.DeleteChunksAsync`
- [ ] Verify both migrations run without data loss on the seeded dev instance (document result in PR)

---

## Phase 12 — `VectorIndexSyncService` refactor (conditional)

**Issue**: [#109](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/109)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 10, Phase 11

- [ ] Inject `IChunkingService` into `src/BookStack.Mcp.Server/services/VectorIndexSyncService.cs` constructor
- [ ] Replace single-call `embeddingGenerator.GenerateAsync([fullPage.Html])` with chunking loop per sync-loop design in spec
- [ ] Implement page format selection: prefer `page.Markdown` when `Editor == "markdown"` and `Markdown` is not null/empty; fall back to `Html`; log input format at `Debug` level
- [ ] Implement DrawIO detection + warning log: if a `<div>`/`<figure>` matching the DrawIO signature is encountered and the configured stripping regex does not match, log `Warning`
- [ ] Call `IVectorStore.DeleteChunksAsync(pageId)` before upserting new chunks
- [ ] Add `DrawIOStrippingPattern` key to `appsettings.json` `VectorSearch` section (configurable regex, no redeployment required)
- [ ] Enforce ChunkSize=0 bypass: when `Chunking.ChunkSize == 0`, skip chunking and use existing single-call path

---

## Phase 13 — `SearchAsync` deduplication and `VectorSearchOptions.Chunking` config (conditional)

**Issue**: [#108](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/108)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 10, Phase 11

- [ ] Update `PgVectorStore.SearchAsync` to deduplicate by `page_id`: return only the highest-scoring chunk per page; `topN` limit applies after deduplication
- [ ] Update `SqliteVecStore.SearchAsync` with same deduplication logic
- [ ] Populate `VectorSearchResult.ChunkIndex` and `VectorSearchResult.Excerpt` (≤ 300 chars of the winning chunk text)
- [ ] Add input validation for `ChunkOptions`: `ChunkSize` ∈ [64, 8192], `ChunkOverlap` ∈ [0, ChunkSize/2], `MaxChunksPerDocument` ∈ [1, 500]; validate at DI registration time in `VectorSearchServiceCollectionExtensions.cs`
- [ ] Add TUnit tests for deduplication logic using `InMemoryVectorStore` with multi-chunk fixture data

---

## Phase 14 — Re-run golden-dataset evaluation and integration tests (conditional)

**Issue**: [#110](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/110)
**Tracks**: Part of #3 (conditional)
**Depends on**: Phase 12, Phase 13

- [ ] Re-run the Phase 3/4 evaluation harness against the Phase 2 implementation
- [ ] Verify all three metrics (`Recall@1 ≥ 0.60`, `Recall@3 ≥ 0.75`, `MRR ≥ 0.65`) improve vs. Phase 1 baseline (per spec AC)
- [ ] Commit updated `docs/features/semantic-search-chunking/evaluation-report-phase2.md` to the PR
- [ ] Write TUnit integration test verifying `ChunkSize=0` produces identical results to pre-Phase-2 baseline (`InMemoryVectorStore` fixture)
- [ ] Write TUnit integration test: page with 3 chunks — `SearchAsync` returns page at most once, excerpt is from highest-scoring chunk
- [ ] Verify Postgres + SQLite migrations run without data loss on seeded dev instance (re-validate if not already done in Phase 11)
- [ ] Update spec status to `Implemented`
