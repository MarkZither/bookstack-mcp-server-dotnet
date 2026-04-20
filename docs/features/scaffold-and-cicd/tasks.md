# Task Decomposition: .NET 10 Solution Scaffold & CI/CD

**Feature**: FEAT-0014
**Parent Issue**: [#14](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/14)
**Decomposed**: 2026-04-20
**Status**: Tasks created on GitHub Issues

---

## Task List

Tasks are ordered by dependency. Each task is independently committable.

### Phase 1 — Setup

- [ ] [Task 1] Create `global.json` SDK pin (.NET 10) → [#21](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/21)

### Phase 2 — Foundational

- [ ] [Task 2] Create `.editorconfig` encoding project formatting rules → [#19](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/19)
- [ ] [P] [Task 3] Create solution file and all project files → [#20](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/20)

### Phase 3 — Core Scaffold

- [ ] [Task 4] Create stub source files (`Program.cs` and `PlaceholderTest.cs`) → [#22](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/22)
  - Depends on: #20, #19

### Phase 4 — CI/CD

- [ ] [Task 5] Create GitHub Actions CI workflow (`.github/workflows/ci.yml`) → [#23](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/23)
  - Depends on: #21, #20, #22

### Phase 5 — Polish

- [ ] [Task 6] Update README with CI badge and Quickstart section → [#24](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/24)
  - Depends on: #23

---

## Dependency Graph

```
#21 (global.json)
     └─► #23 (CI workflow)
              └─► #24 (README)

#19 (.editorconfig)  ──┐
#20 (solution files) ──┼─► #22 (stub files) ─► #23 (CI workflow)
```

---

## Summary

| Issue | Title | Priority | Labels |
| --- | --- | --- | --- |
| [#21](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/21) | Create `global.json` SDK pin | P1 | core |
| [#19](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/19) | Create `.editorconfig` | P1 | core |
| [#20](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/20) | Create solution and project files | P1 | core |
| [#22](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/22) | Create stub source files | P1 | core |
| [#23](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/23) | Create GitHub Actions CI workflow | P1 | core, ci-cd |
| [#24](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/24) | Update README | P1 | core, docs |

**Total tasks**: 6 | **All P1** | **Missing ADRs**: None
