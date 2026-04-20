# Structure (Cache)

> **Source of truth**: [GitHub Issues](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues).
> Check the board for current state. This file is a local cache.
>
> **Platform configured in**: `.memory/board-config.md` → GitHub Issues

## Summary

4 epics · 14 features · dependency graph below

## Board Hierarchy

| Issue | Type | Title | Parent | Priority | Risk | Labels |
|-------|------|-------|--------|----------|------|--------|
| [#1](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/1) | Epic | Core MCP Server | — | — | — | epic, core, p1 |
| [#14](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/14) | Feature | .NET 10 Solution Scaffold & CI/CD | #1 | P1 | Low | feature, core, p1 |
| [#18](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/18) | Feature | BookStack API v25 Client | #1 | P1 | Low–Med | feature, core, p1 |
| [#8](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/8) | Feature | MCP stdio Transport | #1 | P1 | Low | feature, core, p1 |
| [#17](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/17) | Feature | Streamable HTTP Transport | #1 | P2 | Medium | feature, core, p2 |
| [#2](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/2) | Epic | MCP Tools & Resources | — | — | — | epic, tools-resources, p1 |
| [#7](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/7) | Feature | Content Tools & Resources — Books, Chapters, Pages, Shelves | #2 | P1 | Medium | feature, tools-resources, p1 |
| [#9](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/9) | Feature | User & Role Management Tools | #2 | P1 | Low | feature, tools-resources, p1 |
| [#16](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/16) | Feature | Attachments & Images Tools | #2 | P1 | Low | feature, tools-resources, p1 |
| [#6](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/6) | Feature | Admin & Audit Tools — Audit Log, Recycle Bin, Permissions | #2 | P1 | Low | feature, tools-resources, p1 |
| [#12](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/12) | Feature | Search Tool & Resources | #2 | P1 | Low | feature, tools-resources, p1 |
| [#10](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/10) | Feature | Server-Info & System Tools | #2 | P1 | Low | feature, tools-resources, p1 |
| [#3](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/3) | Epic | Vector Search | — | — | — | epic, vector-search, p2 |
| [#5](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/5) | Feature | Vector Search Engine | #3 | P2 | **High** | feature, vector-search, p2 |
| [#11](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/11) | Feature | Semantic Search MCP Tool | #3 | P2 | Medium | feature, vector-search, p2 |
| [#4](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/4) | Epic | Marketplace Distribution | — | — | — | epic, distribution, p3 |
| [#15](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15) | Feature | VS Code Extension Packaging | #4 | P3 | Medium | feature, distribution, p3 |
| [#13](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/13) | Feature | Visual Studio Extension Packaging | #4 | P3 | Medium | feature, distribution, p3 |

## Dependency Graph

```
#14 (.NET 10 Scaffold) ──────────────────────────────────────────── P1 [start here]
  └─► #18 (BookStack API v25 Client) ─────────────────────────── P1
        └─► #8 (MCP stdio Transport) ──────────────────────────── P1
              ├─► #7  (Content Tools & Resources)  ──────────── P1
              ├─► #9  (User & Role Mgmt Tools)     ──────────── P1
              ├─► #16 (Attachments & Images Tools)  ──────────── P1
              ├─► #6  (Admin & Audit Tools)         ──────────── P1
              ├─► #12 (Search Tool & Resources)     ──────────── P1
              ├─► #10 (Server-Info & System Tools)  ──────────── P1
              └─► #17 (Streamable HTTP Transport)   ──────────── P2
                        │
                        └─► [all tools available]
                              │
                              ├─► #5  (Vector Search Engine)  ─ P2 [ADR required first]
                              │     └─► #11 (Semantic Search MCP Tool) ── P2
                              │
                              ├─► #15 (VS Code Extension)  ─── P3
                              └─► #13 (Visual Studio Ext.)  ─── P3 [after #15]
```

## MVP Boundary

P1 features (#14, #18, #8, #7, #9, #16, #6, #12, #10) = **full TypeScript parity** — KPI 1.

P2 features (#17, #5, #11) = vector search — KPI 2.

P3 features (#15, #13) = marketplace distribution — KPI 3.

## Open Questions (Blocking #5)

- [ ] Vector database selection (Microsoft.SemanticKernel memory / Qdrant / SQLite-vec)
- [ ] Embedding model selection and configuration (local ONNX / Azure OpenAI / OpenAI API)
- [ ] Incremental index refresh strategy
- [ ] VS Code vs. Visual Studio VSIX packaging strategy (shared manifest?)

## Notes

- Issue numbers were assigned by GitHub in parallel-creation order; they do not reflect logical sequence.
- Cross-references in feature bodies use original planned numbers with title in parentheses for clarity.
- No cyclic dependencies detected.
- At least one P1 feature exists for each epic that has P1 features.
