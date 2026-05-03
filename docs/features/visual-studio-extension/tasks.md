# Tasks: FEAT-0019 — Visual Studio 2025 Extension Packaging

**Spec**: [spec.md](spec.md)  
**Plan**: [plan.md](plan.md)  
**Parent Epic**: [#4 — Marketplace Distribution](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/4)  
**Parent Feature Issue**: [#19](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/19)

---

## Phase 1 — Setup

- [ ] **Task 1** — Bootstrap `visual-studio-extension/` project skeleton
  [#94](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/94)
  _Create `.csproj`, `.sln`, `source.extension.vsixmanifest`, placeholder icon, initial `CHANGELOG.md`, and `.gitignore` update. Unblocked — start here._
  - **Acceptance**: `dotnet build visual-studio-extension/BookStack.Mcp.VsExtension.csproj` on `windows-latest` produces a `.vsix` with no errors.
  - **Dependencies**: none.

---

## Phase 2 — Core Extension Components

- [ ] **Task 2** — Implement `BookStackMcpPackage` AsyncPackage entry point
  [#92](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/92)
  _Create `BookStackMcpPackage.cs` inheriting `AsyncPackage`; wire `[ProvideOptionPage]` and `[ProvideAutoLoad]`; call `McpRegistrationService` from `InitializeAsync`._
  - **Acceptance**: Extension loads in Experimental VS Instance (F5) without errors; "BookStack MCP Server" page visible under Tools > Options.
  - **Dependencies**: Task 1.

- [ ] **[P] Task 3** — Implement `BookStackOptionsPage` Tools > Options dialog
  [#93](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/93)
  _Create `Options/BookStackOptionsPage.cs` inheriting `DialogPage` with `BookStackUrl`, `TokenId`, and `TokenSecret` string properties._
  - **Acceptance**: Three labelled fields visible in Tools > Options; values survive VS restart.
  - **Dependencies**: Task 1. Parallelisable with Task 2.

- [ ] **Task 4** — Implement `McpRegistrationService` (mcp.json write/merge/remove)
  [#95](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/95)
  _Create `Services/McpRegistrationService.cs`: `RegisterAsync` writes/updates the `"bookstack"` entry in `%APPDATA%\Microsoft\VisualStudio\mcp.json`; `UnregisterAsync` removes it. `TokenSecret` must never be logged._
  - **Acceptance**: `mcp.json` updated on activation; warning written to VS Output if any setting is blank; Token Secret absent from all diagnostic channels.
  - **Dependencies**: Tasks 2, 3.

- [ ] **[P] Task 5** — Bundle win-x64 server binary in VSIX package
  [#99](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/99)
  _Add `<Asset>` element to `source.extension.vsixmanifest` and `<Content IncludeInVSIX>` item to `.csproj` for `bin\BookStack.Mcp.Server.exe`._
  - **Acceptance**: The built `.vsix` contains `bin/BookStack.Mcp.Server.exe`; accessible at runtime via `extensionInstallDir\bin\`.
  - **Dependencies**: Task 1. Parallelisable with Tasks 2–4.

---

## Phase 3 — Documentation

- [ ] **[P] Task 6** — Marketplace README, CHANGELOG, and local dev guide
  [#97](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/97)
  _Create `visual-studio-extension/README.md` (Marketplace listing) and update `visual-studio-extension/CHANGELOG.md` (0.1.0 entry); add local dev guide under `docs/features/visual-studio-extension/`._
  - **Acceptance**: `README.md` and `CHANGELOG.md` present; quick-start steps verified against a working install.
  - **Dependencies**: Tasks 2–4 (steps must be verifiable). Parallelisable with Task 5.

---

## Phase 4 — CI / Release

- [ ] **Task 7** — CI release workflow — VSIX build, Certum signing, and Marketplace publish
  [#98](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/98)
  _Extend `.github/workflows/release.yml` with `build-vs-extension` job (windows-latest, SHA-256 verify, dotnet build, SimplySign PE + VSIX signing) and `publish-vs-marketplace` job (GitHub Release attachment + VsixPublisher). Secrets: `CERTUM_LOGIN`, `CERTUM_TOTP_SECRET`, `CERTUM_CERT_FINGERPRINT`, `VS_MARKETPLACE_PAT`._
  - **Acceptance**: `v*.*.*` tag push produces a signed `.vsix` attached to the GitHub Release; Marketplace version matches the tag.
  - **Dependencies**: Tasks 1–5 (VSIX must build). Repository secrets must be configured.

---

## Phase 5 — Acceptance

- [ ] **Task 8** — Manual acceptance testing checklist
  [#96](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/96)
  _Execute the full ten-item test checklist from the spec against an F5 Experimental VS Instance and a sideloaded VSIX. Update spec status from `Draft` to `Approved` on completion._
  - **Acceptance**: All ten spec acceptance criteria verified; spec status set to `Approved`.
  - **Dependencies**: Tasks 1–7.

---

## Issue Summary

| # | Task | Issue | Labels |
|---|------|-------|--------|
| 1 | Bootstrap project skeleton | [#94](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/94) | enhancement, visual-studio-extension |
| 2 | `BookStackMcpPackage` AsyncPackage | [#92](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/92) | enhancement, visual-studio-extension |
| 3 | `BookStackOptionsPage` Options dialog | [#93](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/93) | enhancement, visual-studio-extension |
| 4 | `McpRegistrationService` | [#95](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/95) | enhancement, visual-studio-extension, security |
| 5 | Bundle win-x64 binary in VSIX | [#99](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/99) | enhancement, visual-studio-extension |
| 6 | Marketplace README & docs | [#97](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/97) | documentation, visual-studio-extension |
| 7 | CI workflow — VSIX build + signing + publish | [#98](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/98) | enhancement, visual-studio-extension, ci-cd |
| 8 | Manual acceptance testing | [#96](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/96) | testing, visual-studio-extension |
