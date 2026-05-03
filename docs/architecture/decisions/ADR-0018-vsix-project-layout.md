# ADR-0018: VSIX Extension Project Layout and Target Framework

**Status**: Proposed
**Date**: 2026-05-03
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The Visual Studio 2025 extension (FEAT-0019) introduces a new C# VSIX project into the repository. ADR-0002
established a canonical `src/` + `tests/` split for all C# projects. A decision is needed on whether the VSIX
project belongs inside `src/` or outside it, and on which .NET target framework moniker (TFM) to use.

Relevant constraints:

1. **ADR-0002** placed all C# source projects under `src/` with namespaces mirroring directory paths. All existing
   `src/` projects target `net10.0` (cross-platform).
2. **The VS Code extension** (`FEAT-0015`) already lives at `vscode-extension/` at the repository root — outside
   `src/`. It is a Node.js / TypeScript project, so it was never a candidate for `src/`.
3. **The VSIX project is packaging/IDE-integration code**, not a server component. It bundles a pre-built server
   binary and registers the MCP server with VS 2025; it contains no business logic shared with `src/` projects.
4. **Visual Studio SDK requires Windows** and a Windows-compatible TFM. The project must target `net10.0-windows`
   (or `net48`, the classic VSIX TFM). `net10.0` without the `-windows` suffix cannot reference VS SDK packages.
5. **`dotnet build` from the repo root** (the pattern established by ADR-0002 and ADR-0003) must not break for
   developers on non-Windows platforms, since CI runs on `ubuntu-latest`. A `net10.0-windows` project added to the
   solution file would fail to build on Linux without explicit conditional exclusion.
6. **The VSIX project is published to a different marketplace** (Visual Studio Marketplace, not VS Code Marketplace)
   with different toolchain requirements (`dotnet build` producing a `.vsix` rather than `vsce package`).

Three layout options were considered:

1. **Place VSIX project in `src/`** (e.g., `src/BookStack.Mcp.VsExtension/`) and add it to the solution file.
2. **Place VSIX project at `visual-studio-extension/` root level** (paralleling `vscode-extension/`), and add it to
   the solution file with a condition that excludes it from non-Windows builds.
3. **Place VSIX project at `visual-studio-extension/` root level** in a separate solution file that is not the
   repository root solution, keeping the root solution cross-platform.

## Decision

We will place the VSIX project at **`visual-studio-extension/BookStack.Mcp.VsExtension.csproj`** — at the
repository root level in its own folder, paralleling `vscode-extension/`. It will **not** be added to the root
`BookStack.Mcp.Server.sln`; instead, a separate `BookStack.Mcp.VsExtension.sln` within `visual-studio-extension/`
will include it alongside a project reference to `../src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj` for
integrated F5 debugging in VS 2025.

The project targets **`net10.0-windows`** — the minimum TFM that supports both the .NET 10 runtime features used by
the extension code and the Windows-specific VS SDK and `System.Windows.Forms`/`System.Drawing` types required by
`DialogPage`.

## Rationale

- **Parallel to `vscode-extension/`**: the repository already has precedent for a first-class packaging project
  living outside `src/` at the root level. The `vscode-extension/` directory is treated as a peer to `src/` and
  `tests/`, not as a library. The VSIX project has the same character: it is a packaging and IDE-integration
  artefact, not a reusable library.
- **Separate solution file keeps the root solution cross-platform**: the root `BookStack.Mcp.Server.sln` is used in
  CI (`ubuntu-latest`) for every `dotnet build` / `dotnet test` / `dotnet format` invocation. Including a
  `net10.0-windows` project in that solution would require platform-conditional `<Condition>` on every build step
  or a solution filter. A separate `BookStack.Mcp.VsExtension.sln` is simpler and does not affect existing CI.
- **`net10.0-windows`** is the correct TFM for a .NET 10 project that references VS SDK NuGet packages
  (`Microsoft.VisualStudio.SDK`, `Microsoft.VSSDK.BuildTools`). These packages declare `net10.0-windows` as a
  target in their TFM compatibility matrix. Using `net48` (the classic VSIX TFM) is possible but would preclude
  using `async`/`await` natively, `System.Text.Json`, and nullable reference types — all of which the extension
  code uses.
- **Not in `src/`**: placing a Windows-only project inside `src/` would break the invariant that `src/` contains
  cross-platform server components. It would also make `dotnet build src/` produce a platform-specific failure on
  Linux without additional solution filter configuration.

## Alternatives Considered

### Option A: Place VSIX project in `src/` and add to root solution

- **Pros**: Consistent with ADR-0002's `src/` convention; single solution file covers all C# projects.
- **Cons**: Breaks `dotnet build` on Linux (`net10.0-windows` target fails on non-Windows platforms); requires
  per-job `<Condition>` guards in CI; `src/` would contain a packaging project rather than a server component,
  muddying the directory's purpose; `dotnet format` in CI would need to exclude the Windows-only project.
- **Why rejected**: Cross-platform CI breakage is unacceptable; the project character does not belong in `src/`.

### Option B: Place VSIX project at root level, add to root solution with platform condition

- **Pros**: Single `.sln` file; contributors opening the root solution in VS 2025 on Windows see all projects.
- **Cons**: `<Condition>` on a solution project reference is not well-supported in MSBuild — it works for
  `<ProjectReference>` in individual projects but not for `<Project>` entries in `.sln` files; CI would still need
  to exclude the project explicitly; VS 2025 on Windows would show the project; VS on Linux (Rider, `dotnet build`)
  would need the condition to be honoured correctly.
- **Why rejected**: MSBuild solution-level conditions are fragile; the separate solution approach is cleaner and
  is already established practice in the .NET ecosystem (e.g., Blazor + MAUI repos use separate solution files for
  platform-specific projects).

### Option C: Target `net48` (classic VSIX TFM)

- **Pros**: Maximum VS SDK compatibility; classic VSIX projects have used `net48` for years; no TFM-related VS SDK
  package compatibility concerns.
- **Cons**: `net48` does not support C# 13, top-level statements, `System.Text.Json` (natively), or nullable
  reference types; `async`/`await` in `AsyncPackage` with .NET Framework requires additional polyfill packages;
  the project would be inconsistent with the rest of the codebase (all `net10.0`); the project team has no
  .NET Framework experience in this codebase.
- **Why rejected**: Developer experience and feature consistency with the rest of the codebase outweigh the
  historic compatibility of `net48`. `net10.0-windows` is fully supported by the current VS SDK packages.

## Consequences

### Positive

- `dotnet build`, `dotnet test`, and `dotnet format` on the root solution remain cross-platform and unaffected.
- `visual-studio-extension/` is a self-contained unit: opening `BookStack.Mcp.VsExtension.sln` in VS 2025 on
  Windows gives developers the full picture (VSIX project + server project reference) for F5 debugging.
- `net10.0-windows` enables all modern C# features, `System.Text.Json`, and async/await that the rest of the
  codebase uses — no dual-framework mental model for contributors.
- Separation mirrors the TypeScript precedent set by `vscode-extension/`, making the repository layout
  self-explanatory: `src/` = server, `vscode-extension/` = VS Code packaging, `visual-studio-extension/` = VS
  packaging.

### Negative / Trade-offs

- Two solution files exist in the repository. Contributors opening the root solution on Windows do not see the VSIX
  project; they must open `visual-studio-extension/BookStack.Mcp.VsExtension.sln` for the full VS 2025 extension
  development experience.
- CI must explicitly reference `visual-studio-extension/BookStack.Mcp.VsExtension.csproj` (not the root solution)
  when building and packaging the VSIX.
- `net10.0-windows` means the VSIX project cannot be built on Linux without the Windows Compatibility Pack, which
  is fine for CI (the VSIX build job targets `windows-latest`).

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
- [ADR-0003: CI/CD GitHub Actions Strategy](ADR-0003-cicd-github-actions.md)
- [ADR-0011: VS Code Extension Binary Bundling Strategy](ADR-0011-vscode-extension-binary-bundling.md)
- [ADR-0017: Visual Studio 2025 MCP Server Registration Strategy](ADR-0017-vs2025-mcp-registration.md)
