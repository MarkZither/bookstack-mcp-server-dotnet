# Task Decomposition: Content Tools and Resources — Books, Chapters, Pages, Shelves

**Feature**: FEAT-0007
**Parent Issue**: [#7](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/7)
**Decomposed**: 2026-04-20
**Status**: All tasks completed — merged via PR #51

---

## Task List

Tasks are ordered by dependency. Each task is independently committable.

### Phase 1 — Tool Handler Implementations

- [X] [Task 1] Implement `BookToolHandler` — 6 CRUD tools (`list`, `read`, `create`, `update`, `delete`, `export`) → [#45](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/45)
  - Establishes `_jsonOptions`, error-handling, and validation patterns for Tasks 2–5
- [X] [P] [Task 2] Implement `ChapterToolHandler` — 6 CRUD tools (`list`, `read`, `create`, `update`, `delete`, `export`) → [#43](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/43)
  - Depends on: #45 (follow patterns established in Task 1)
- [X] [P] [Task 3] Implement `PageToolHandler` — 6 CRUD tools (`list`, `read`, `create`, `update`, `delete`, `export`) → [#44](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/44)
  - Depends on: #45 (follow patterns established in Task 1)
- [X] [P] [Task 4] Implement `ShelfToolHandler` — 5 CRUD tools (`list`, `read`, `create`, `update`, `delete`) → [#46](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/46)
  - Depends on: #45 (follow patterns established in Task 1)

### Phase 2 — Resource Handler Implementations

- [X] [Task 5] Implement resource handlers — `BookResourceHandler`, `ChapterResourceHandler`, `PageResourceHandler`, `ShelfResourceHandler` (collection + `{id}` methods) → [#49](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/49)
  - Depends on: #45, #43, #44, #46

### Phase 3 — Tests

- [X] [Task 6] Tests — `BookToolHandlerTests` (10 test cases) → [#47](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/47)
  - Depends on: #45
- [X] [P] [Task 7] Tests — `ChapterToolHandlerTests`, `PageToolHandlerTests`, `ShelfToolHandlerTests` (11 test cases) → [#50](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/50)
  - Depends on: #43, #44, #46, #47 (follow test setup pattern from Task 6)
- [X] [P] [Task 8] Tests — `BookResourceHandlerTests`, `PageResourceHandlerTests` (4 test cases) → [#48](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/48)
  - Depends on: #49, #47 (follow test setup pattern from Task 6)

---

## Dependency Graph

```
#45 (BookToolHandler)
     ├─► #43 (ChapterToolHandler)
     ├─► #44 (PageToolHandler)
     └─► #46 (ShelfToolHandler)
               └─► #49 (resource handlers)
#45 ──────────────► #47 (BookToolHandler tests)
#43, #44, #46 ─────► #50 (Chapter/Page/Shelf tests)
#49 ──────────────► #48 (resource handler tests)
#47 ──────────────► #50, #48 (test setup pattern)
```

---

## Summary

| Issue | Title | Plan Task | Tests |
| --- | --- | --- | --- |
| [#45](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/45) | Implement `BookToolHandler` — 6 CRUD tools | Task 1 | #47 |
| [#43](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/43) | Implement `ChapterToolHandler` — 6 CRUD tools | Task 2 | #50 |
| [#44](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/44) | Implement `PageToolHandler` — 6 CRUD tools | Task 3 | #50 |
| [#46](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/46) | Implement `ShelfToolHandler` — 5 CRUD tools | Task 4 | #50 |
| [#49](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/49) | Implement resource handlers (4 handlers, 8 methods) | Task 5 | #48 |
| [#47](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/47) | Tests — `BookToolHandler` (10 test cases) | Task 6 | — |
| [#50](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/50) | Tests — `ChapterToolHandler`, `PageToolHandler`, `ShelfToolHandler` (11 test cases) | Task 7 | — |
| [#48](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/48) | Tests — resource handlers `BookResourceHandler`, `PageResourceHandler` (4 test cases) | Task 8 | — |

**Total sub-issues**: 8 | **Total test cases**: 25 | **Missing ADRs**: None
