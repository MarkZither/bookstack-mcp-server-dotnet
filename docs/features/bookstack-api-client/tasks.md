# Task Decomposition: BookStack API v25 HTTP Client

**Feature**: FEAT-0018
**Parent Issue**: [#18](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/18)
**Decomposed**: 2026-04-20
**Status**: All tasks completed — merged via PR #42

---

## Task List

Tasks are ordered by dependency. Each task is independently committable.

### Phase 1 — Configuration & Options

- [X] [Task 1] `BookStackApiClientOptions` POCO + `IValidateOptions` validator → [#26](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/26)

### Phase 2 — Shared Types & Exceptions

- [X] [P] [Tasks 2–3] `BookStackApiException` + `ListResponse<T>` / `ExportFormat` / `ContentType` shared types → [#27](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/27)
  - Depends on: #26

### Phase 3 — Response Models

- [X] [P] [Task 4] Response model types (all 13 entity model files under `api/models/`) → [#28](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/28)
  - Depends on: #27

### Phase 4 — Interface

- [X] [Task 5] `IBookStackApiClient` interface (47 typed async methods) → [#29](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/29)
  - Depends on: #27, #28

### Phase 5 — Delegating Handlers

- [X] [P] [Task 6] `AuthenticationHandler` (`DelegatingHandler` — inject Authorization header) → [#30](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/30)
  - Depends on: #26
- [X] [P] [Task 7] `RateLimitHandler` (`DelegatingHandler` — X-RateLimit-* header strategy) → [#31](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/31)

### Phase 6 — Implementation

- [X] [Task 8] `BookStackApiClient` core partial + Books partial → [#32](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/32)
  - Depends on: #26, #27, #28, #29
- [X] [Task 9–12] Remaining entity partials (Chapters, Pages, Shelves, Users, Roles, Attachments, Images, Search, RecycleBin, Permissions, AuditLog, System) → [#33](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/33)
  - Depends on: #32

### Phase 7 — DI Registration

- [X] [Task 13] `AddBookStackApiClient` DI extension method + startup validation → [#34](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/34)
  - Depends on: #26, #29, #30, #31, #32

### Phase 8 — Tests

- [X] [Tasks 14–22] Test infrastructure + unit tests (MockHttpMessageHandler, all entity groups, handlers, DI) → [#35](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/35)
  - Depends on: #26, #27, #28, #29, #30, #31, #32, #33, #34

---

## Dependency Graph

```
#26 (Options)
     ├─► #27 (Shared types)
     │        ├─► #28 (Models)
     │        │        └─► #29 (Interface)
     │        │                  └─► #32 (Client core + Books)
     │        │                             └─► #33 (Entity partials)
     │        └─► #29 (Interface)
     ├─► #30 (AuthenticationHandler)
     └─► #34 (DI) ◄─── #29, #30, #31, #32

#31 (RateLimitHandler) ─────────────────► #34 (DI)

#26..#34 ────────────────────────────────► #35 (Tests)
```

---

## Summary

| Issue | Title | Plan Tasks | Labels |
| --- | --- | --- | --- |
| [#26](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/26) | `BookStackApiClientOptions` POCO + `IValidateOptions` | Task 1 | core, p1, feature |
| [#27](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/27) | `BookStackApiException` + shared types | Tasks 2–3 | core, p1, feature |
| [#28](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/28) | Response model types (13 entity files) | Task 4 | core, p1, feature |
| [#29](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/29) | `IBookStackApiClient` interface (47 methods) | Task 5 | core, p1, feature |
| [#30](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/30) | `AuthenticationHandler` | Task 6 | core, p1, feature |
| [#31](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/31) | `RateLimitHandler` | Task 7 | core, p1, feature |
| [#32](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/32) | `BookStackApiClient` core + Books partial | Task 8 | core, p1, feature |
| [#33](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/33) | Remaining entity partials | Tasks 9–12 | core, p1, feature |
| [#34](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/34) | `AddBookStackApiClient` DI extension | Task 13 | core, p1, feature |
| [#35](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/35) | Test infrastructure + all unit tests | Tasks 14–22 | core, p1, feature |

**Total sub-issues**: 10 | **Plan tasks covered**: 22 | **Missing ADRs**: None
