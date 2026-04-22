# ADR-0011: VS Code Extension Binary Bundling Strategy

**Status**: Accepted
**Date**: 2026-04-22
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The VS Code extension (FEAT-0015) must launch the BookStack MCP server as a child process over stdio. VS Code's `mcpServers` contribution point requires a `command` that resolves to an executable on the local filesystem.

Three distribution strategies exist for shipping the .NET server binary alongside the extension:

1. **Bundle pre-built self-contained binaries inside the VSIX** — users need no .NET runtime.
2. **Require .NET runtime on PATH** — users must install .NET 10 separately; the extension invokes `dotnet run` or a framework-dependent binary.
3. **Download binary on first activation** — the VSIX is small; the extension fetches the correct binary from a GitHub Release on first use.

Additional constraints:

- The primary developer and CI environment is Linux (ubuntu-latest). macOS is not available as a test environment and is explicitly out of scope for v1.
- The extension must work for Windows (win-x64) and Linux (linux-x64) users.
- The VS Code extension API does not provide a built-in package manager or download mechanism; option 3 requires significant custom activation code and introduces a network dependency at install time.
- Self-contained .NET 10 binaries produced with `PublishSingleFile=true` are approximately 50 MB each per platform.

## Decision

We will bundle **pre-built, self-contained single-file binaries** for `win-x64` and `linux-x64` directly inside the VSIX package.

- The server project will be published with `dotnet publish -r <rid> --self-contained true -p:PublishSingleFile=true` for each target platform.
- The CI release workflow (`.github/workflows/release.yml`) will build both platform binaries in parallel on `ubuntu-latest` and copy them into `vscode-extension/bin/` before running `vsce package`.
- `extension.ts` will resolve the binary path at activation time based on `process.platform`: `win32` → `bin/bookstack-mcp-server.exe`, `linux` → `bin/bookstack-mcp-server-linux`. Any other platform surfaces a clear unsupported-platform error and does not attempt to spawn a process.
- The API token is assembled from two separate settings (`bookstack.tokenId` + `bookstack.tokenSecret`) and passed as the environment variable `BOOKSTACK_TOKEN_SECRET` in `tokenId:tokenSecret` format — it is never written to any log or output channel.

macOS (osx-x64 / osx-arm64) is deferred to a future release pending a test environment.

## Rationale

- **No runtime prerequisite** is the primary UX goal: a typical VS Code user should not need to know what .NET is. Bundling binaries achieves this unconditionally.
- **Avoids network dependency at activation**: option 3 (download on first use) would require robust error handling for offline environments, corporate proxies, and GitHub rate limits — complexity not justified for v1.
- **CI is Linux-only**: `dotnet publish` cross-compiles win-x64 and linux-x64 from an `ubuntu-latest` runner without additional tooling. macOS cross-compilation from Linux is unsupported by the .NET SDK; deferring osx support until a macOS runner is available avoids an untested build path.
- **VSIX size is acceptable**: two binaries at ~50 MB each yields a ~100 MB VSIX, within the VS Code Marketplace's 200 MB limit and the spec's 100 MB target.
- **Single-file publish** avoids shipping a directory of assemblies, which simplifies `.vscodeignore` patterns and produces a clean `bin/` layout.

## Alternatives Considered

### Option A: Require .NET runtime on PATH

- **Pros**: Tiny VSIX (~5 KB); no per-platform build step.
- **Cons**: Requires users to install .NET 10 and ensure it is on PATH; framework-dependent publish requires correct runtime version to be present; poor install experience for non-.NET developers; debugging which .NET version is active is a common support burden.
- **Why rejected**: Violates the zero-prerequisite UX goal.

### Option B: Download binary on first activation

- **Pros**: Small VSIX; binary can be updated independently of the extension.
- **Cons**: Requires network access at activation time; significant custom download/verify/cache code in `extension.ts`; SHA-256 verification is mandatory to avoid supply-chain risk; fails in air-gapped or restricted corporate environments; GitHub Release asset URLs must be stable across renames.
- **Why rejected**: Complexity and network dependency are not justified for v1; the download logic would itself require testing across network conditions.

### Option C: Bundle all three platforms (win-x64, linux-x64, osx-x64)

- **Pros**: macOS users get the same zero-prerequisite experience.
- **Cons**: macOS binaries cannot be tested without a macOS machine or runner; Gatekeeper and Notarization requirements add significant CI complexity (Apple Developer ID certificate, `codesign`, `xcrun notarytool`); adds ~50 MB to VSIX without verified correctness.
- **Why rejected**: No test environment available. Shipping an untested binary that may be silently quarantined by Gatekeeper is worse than showing a clear "platform not supported" message. Deferred to a future ADR when macOS CI is available.

## Consequences

### Positive

- Users on Windows and Linux install the extension and get a working MCP server with no prerequisites.
- CI is straightforward: a single `ubuntu-latest` runner cross-compiles both targets.
- Binary provenance is explicit: every release's VSIX contains binaries built from the exact tagged commit.
- No outbound network calls from the extension itself — simplifies security review.

### Negative / Trade-offs

- VSIX is ~100 MB (two self-contained binaries). Extension updates re-download the full VSIX even if only the TypeScript activation code changed.
- macOS users see an "unsupported platform" error until osx support is added in a future release.
- Binary code-signing for Windows (`signtool`) is not implemented in v1; Windows SmartScreen may warn on first run of the extracted binary. This is an acceptable v1 trade-off.
- VSIX size grows linearly with each new platform added in future releases.

## Related ADRs

- [ADR-0002](ADR-0002-solution-structure.md) — solution structure (the `vscode-extension/` folder is a new root-level project alongside `src/`)
- [ADR-0003](ADR-0003-cicd-github-actions.md) — CI/CD with GitHub Actions (the release workflow extends the existing CI pipeline)
- [ADR-0009](ADR-0009-dual-transport-entry-point.md) — stdio transport is the mechanism the extension uses to communicate with the bundled binary
