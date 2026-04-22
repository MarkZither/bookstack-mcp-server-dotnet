# Task Decomposition: VS Code Extension Packaging

**Feature**: FEAT-0015
**Parent Issue**: [#15](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15)
**Decomposed**: 2026-04-22
**Status**: In progress

---

## Pre-Implementation Checklist

- [ ] Marketplace publisher account `MarkZither` exists at https://marketplace.visualstudio.com/manage
- [ ] `VSCE_PAT` secret added to repository Actions secrets (`Settings → Secrets → Actions`)

---

## Task List

Tasks are ordered by dependency. Each task is independently committable.

### Phase 1 — Project Skeleton

- [ ] [Task 1] Bootstrap `vscode-extension/` project skeleton — `package.json`, `tsconfig.json`, `.vscodeignore`, `.eslintrc.json`, `.gitignore` entries → [#55](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/55)

### Phase 2 — Core Logic

- [ ] [Task 2] Implement `extension.ts` — settings validation, platform binary resolution, token concatenation, error notifications → [#56](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/56)
  - Depends on: #55
- [ ] [Task 3] Add F5 debug configuration — `launch.json` + `tasks.json` → [#57](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/57)
  - Depends on: #55

### Phase 3 — Build Verification

- [ ] [Task 5] Verify `dotnet publish` produces win-x64 and linux-x64 single-file binaries → [#58](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/58)
  - Depends on: #56 (binary path must match what extension.ts expects)

### Phase 4 — Marketplace Assets

- [ ] [Task 6] Add marketplace assets — `icon.png`, `README.md`, `CHANGELOG.md` → [#59](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/59)
  - Depends on: #55

### Phase 5 — CI/CD

- [ ] [Task 7] Create `release.yml` — parallel binary builds, VSIX package, GitHub Release, Marketplace publish → [#60](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/60)
  - Depends on: #58, #59
- [ ] [Task 8] Extend `ci.yml` with extension lint job → [#61](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/61)
  - Depends on: #55

---

## Dependency Graph

```
#55 (bootstrap skeleton)
     ├─► #56 (extension.ts)
     │     └─► #58 (verify publish)
     │               └─► #60 (release.yml)
     ├─► #57 (launch.json / F5)
     ├─► #59 (marketplace assets) ─► #60 (release.yml)
     └─► #61 (ci.yml lint job)
```

---

## Summary

| Issue | Title | Plan Task | Labels |
|-------|-------|-----------|--------|
| [#55](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/55) | Bootstrap vscode-extension/ project skeleton | Task 1 | distribution, p1, feature |
| [#56](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/56) | Implement extension.ts activation logic | Task 2 | distribution, p1, feature |
| [#57](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/57) | Add F5 debug configuration | Task 3 | distribution, p1, feature |
| [#58](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/58) | Verify dotnet publish single-file binaries | Task 5 | distribution, p1, feature |
| [#59](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/59) | Add marketplace assets | Task 6 | distribution, p1, feature, docs |
| [#60](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/60) | Create release.yml | Task 7 | distribution, p1, feature, ci-cd |
| [#61](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/61) | Extend ci.yml with extension lint job | Task 8 | distribution, p1, feature, ci-cd |

**Total sub-issues**: 7 | **All P1** | **Missing ADRs**: None
