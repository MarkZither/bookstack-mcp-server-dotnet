# Tasks: Book/Shelf Scope Filtering (FEAT-0054)

**Feature**: Book/Shelf Scope Filtering
**Parent Issue**: [#54](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/54)
**Branch**: `feat/54-scope-filtering`
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**ADR**: [ADR-0014](../../architecture/decisions/ADR-0014-scope-filter-architecture.md)

---

## Phase 1 — Config & DI

**Issue**: [#73 — feat(#54): Config & DI — ScopeFilterOptions, ScopeFilter helper, env var mapping](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/73)

- [ ] T1 — Create `ScopeFilterOptions` class in `src/BookStack.Mcp.Server/config/ScopeFilterOptions.cs`
- [ ] T2 — Create static `ScopeFilter` helper in `src/BookStack.Mcp.Server/config/ScopeFilter.cs`
- [ ] T3 — Extend `MapBookStackEnvVars()` in `src/BookStack.Mcp.Server/Program.cs` with scope env var parsing and regex validation
- [ ] T4 — Register `IOptions<ScopeFilterOptions>` in `src/BookStack.Mcp.Server/api/BookStackServiceCollectionExtensions.cs`

---

## Phase 2 — Tool Handler Filtering

**Issue**: [#74 — feat(#54): Tool handler filtering — books, shelves, search](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/74)
**Depends on**: #73

- [ ] T5 — Inject `IOptions<ScopeFilterOptions>` and apply book filter in `src/BookStack.Mcp.Server/tools/books/BookToolHandler.cs`
- [ ] T6 — Inject and apply shelf filter in `src/BookStack.Mcp.Server/tools/shelves/ShelfToolHandler.cs`
- [ ] T7 — Inject and apply book filter to `SearchResult.Data` in `src/BookStack.Mcp.Server/tools/search/SearchToolHandler.cs`

---

## Phase 3 — Resource Handler Filtering

**Issue**: [#75 — feat(#54): Resource handler filtering — books and shelves resources](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/75)
**Depends on**: #73 (parallel with #74)

- [ ] T8 — Inject and apply book filter in `src/BookStack.Mcp.Server/resources/books/BookResourceHandler.cs`
- [ ] T9 — Inject and apply shelf filter in `src/BookStack.Mcp.Server/resources/shelves/ShelfResourceHandler.cs`

---

## Phase 4 — VS Code Extension

**Issue**: [#76 — feat(#54): VS Code extension — scopedBooks and scopedShelves settings](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/76)
**Independent** (no code dependency on Phases 1–3)

- [ ] T10 — Add `bookstack.scopedShelves` and `bookstack.scopedBooks` settings to `vscode-extension/package.json`
- [ ] T11 — Map settings to `BOOKSTACK_SCOPED_BOOKS` / `BOOKSTACK_SCOPED_SHELVES` env vars in `vscode-extension/src/<spawn-provider>`

---

## Phase 5 — Tests

**Issue**: [#77 — feat(#54): Tests — unit and integration tests for scope filtering](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/77)
**Depends on**: #73, #74, #75

- [ ] T12 — `tests/BookStack.Mcp.Server.Tests/config/ScopeFilterTests.cs` — `MatchesScope` unit tests (numeric ID, slug case-insensitive, empty scope, no match)
- [ ] T13 — `tests/BookStack.Mcp.Server.Tests/tools/BookToolHandlerTests.cs` — scoped/unscoped list, `Total` update, read tool unaffected
- [ ] T14 — `tests/BookStack.Mcp.Server.Tests/tools/ShelfToolHandlerTests.cs` — scoped/unscoped list
- [ ] T15 — `tests/BookStack.Mcp.Server.Tests/tools/SearchToolHandlerTests.cs` — book-type items, items with/without book ref, unscoped pass-through
- [ ] T16 — `tests/BookStack.Mcp.Server.Tests/config/MapBookStackEnvVarsTests.cs` — valid entries, whitespace trim, invalid entry discarded
- [ ] T17 — `tests/BookStack.Mcp.Server.Tests/resources/BookResourceHandlerTests.cs` — scoped `GetBooksAsync`
- [ ] T18 — `tests/BookStack.Mcp.Server.Tests/resources/ShelfResourceHandlerTests.cs` — scoped `GetShelvesAsync`

---

## Summary

| Phase | Issue | Tasks | Status |
|-------|-------|-------|--------|
| 1 — Config & DI | [#73](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/73) | T1–T4 | open |
| 2 — Tool handlers | [#74](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/74) | T5–T7 | open |
| 3 — Resource handlers | [#75](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/75) | T8–T9 | open |
| 4 — VS Code extension | [#76](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/76) | T10–T11 | open |
| 5 — Tests | [#77](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/77) | T12–T18 | open |

**Total tasks**: 18 across 5 issues
**Dependency graph**: #73 → {#74, #75} → #77 · #76 (independent)
