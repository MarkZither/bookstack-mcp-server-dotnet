# Task Decomposition: MCP Server Infrastructure — stdio + Streamable HTTP

**Feature**: FEAT-0008
**Parent Issue**: [#8](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/8)
**Decomposed**: 2026-04-20
**Status**: Tasks created on GitHub Issues

---

## Task List

Tasks are ordered by dependency. Each task is independently committable.

### Phase 1 — Project Setup

- [ ] [Task 1] Update `BookStack.Mcp.Server.csproj` — SDK Web + MCP NuGet packages → [#40](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/40)

### Phase 2 — Composition Root

- [ ] [Task 2] Implement `Program.cs` — transport selection, DI wiring, stdio + HTTP host branches → [#37](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/37)
  - Depends on: #40

### Phase 3 — Handler Stubs

- [ ] [P] [Task 3] Stub 14 tool handler classes under `tools/` with `[McpServerToolType]` attributes → [#38](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/38)
  - Depends on: #40
- [ ] [P] [Task 4] Stub 6 resource handler classes under `resources/` with `[McpServerResourceType]` attributes → [#39](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/39)
  - Depends on: #40

### Phase 4 — Tests

- [ ] [Task 5] Tests — attribute verification, assembly scan counts, tool name non-empty checks → [#41](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/41)
  - Depends on: #38, #39

---

## Dependency Graph

```
#40 (csproj — SDK + packages)
     ├─► #37 (Program.cs)
     ├─► #38 (tool handler stubs)
     └─► #39 (resource handler stubs)
               └─► #41 (tests)
#38 ──────────────► #41 (tests)
```

---

## Summary

| Issue | Title | Plan Task | Labels |
| --- | --- | --- | --- |
| [#40](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/40) | Update `BookStack.Mcp.Server.csproj` — SDK Web + MCP NuGet packages | Task 1 | core, p1, feature |
| [#37](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/37) | Implement `Program.cs` — transport selection, DI wiring, stdio + HTTP host branches | Task 2 | core, p1, feature |
| [#38](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/38) | Stub 14 tool handler classes under `tools/` with `[McpServerToolType]` attributes | Task 3 | core, p1, feature |
| [#39](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/39) | Stub 6 resource handler classes under `resources/` with `[McpServerResourceType]` attributes | Task 4 | core, p1, feature |
| [#41](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/41) | Tests — attribute verification, assembly scan counts, tool name non-empty checks | Task 5 | core, p1, feature |

**Total sub-issues**: 5 | **All P1** | **Missing ADRs**: None
