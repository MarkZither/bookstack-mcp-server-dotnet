# ADR-0017: Visual Studio 2025 MCP Server Registration Strategy

**Status**: Proposed
**Date**: 2026-05-03
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The Visual Studio 2025 extension (FEAT-0019) must register the BookStack MCP server so that VS 2025's built-in
MCP host can discover it and make it available to GitHub Copilot. Visual Studio 2025 (version 18.x) ships with
native MCP server hosting; this is a newer capability that does not exist in VS 2022.

Three registration approaches were evaluated:

1. **User-profile `mcp.json`** — Write a server entry to `%APPDATA%\Microsoft\VisualStudio\mcp.json`. VS 2025 reads
   this file at startup and manages the server process lifecycle directly. This is the documented, supported
   registration mechanism for user-installed MCP servers.
2. **`IVsMcpServerProvider` VS SDK API** — Implement a VS SDK interface that programmatically registers the server
   at extension load time from managed code, without writing a JSON file.
3. **User documentation only** — Provide a README that instructs users to manually edit `mcp.json` after install.

Additional constraints:

- VS 2025 manages the server process lifecycle (start, stop, restart on crash). The extension MUST NOT use
  `Process.Start` to spawn the server directly.
- The server binary path must be resolved at runtime from the extension's install directory, because VSIX install
  paths are not predictable.
- The `mcp.json` entry must include the binary's absolute path and the two environment variables
  (`BOOKSTACK_BASE_URL`, `BOOKSTACK_TOKEN_SECRET`) that the server reads for configuration.
- The extension must clean up the `mcp.json` entry on uninstall to avoid leaving a broken entry pointing to a
  removed binary.

## Decision

We will register the MCP server by **writing a server entry to the user-profile `mcp.json`** at
`%APPDATA%\Microsoft\VisualStudio\mcp.json` during `AsyncPackage.InitializeAsync`. The entry is written (or
updated) every time the package loads, so the binary path stays current after extension upgrades.

The written entry takes the following shape (values resolved at runtime):

```json
{
  "servers": {
    "bookstack": {
      "type": "stdio",
      "command": "<resolved-extension-install-dir>\\bin\\BookStack.Mcp.Server.exe",
      "env": {
        "BOOKSTACK_BASE_URL": "<value from BookStack Options page>",
        "BOOKSTACK_TOKEN_SECRET": "<tokenId>:<tokenSecret>"
      }
    }
  }
}
```

The binary path is resolved via `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` combined with
`Path.Combine(..., "bin", "BookStack.Mcp.Server.exe")`.

If any of the three required settings (URL, Token ID, Token Secret) is blank at package load, the entry is **not**
written; instead, a message is surfaced in the VS Output window directing the user to Tools > Options. This prevents
VS 2025 from attempting to start the server with missing credentials.

The `mcp.json` file is read as a JSON object, the `"bookstack"` key under `"servers"` is added or replaced, and
the file is written back atomically. If the file does not exist, it is created with the minimal structure.

## Rationale

- **User-profile `mcp.json` is the documented mechanism** for registering MCP servers in VS 2025. Using the
  supported approach avoids reliance on undocumented internals that may change across VS 2025 minor versions.
- **`IVsMcpServerProvider` does not exist** as a public VS SDK interface in VS 2025 at the time of this decision.
  The MCP hosting in VS 2025 is intentionally file-driven to allow tool-agnostic server discovery without requiring
  an extension for every server.
- **Manual documentation** would require every user to locate the correct `%APPDATA%` path, create the JSON file
  manually, and resolve the dynamic extension install path themselves — directly contradicting the zero-friction
  install goal stated in the spec.
- **Writing on every package load** ensures the entry is self-healing: if the user manually deletes the entry or
  upgrades the extension (changing the install path), the correct path is restored automatically on the next VS
  startup.
- **Skipping the write on blank settings** prevents VS 2025 from logging recurring process-spawn failures for an
  unconfigured server, which would pollute the Output window and Activity Log.

## Alternatives Considered

### Option A: `IVsMcpServerProvider` VS SDK API

- **Pros**: Pure managed-code registration; no file I/O; version-safe if Microsoft evolves the MCP hosting API.
- **Cons**: This interface does not exist in the VS 2025 public SDK at the time of writing. Implementing against an
  undocumented or future interface would require a private preview SDK dependency and could break on any VS update.
- **Why rejected**: No public API surface available. Cannot be implemented.

### Option B: User documentation only (manual `mcp.json` editing)

- **Pros**: Zero extension code required for registration; zero risk of corrupting the file.
- **Cons**: Requires the user to know the `%APPDATA%` path, resolve the dynamic VSIX install directory, and
  correctly format JSON. Direct contradiction of the zero-friction install goal. Equivalent to requiring manual
  `settings.json` editing for the VS Code extension — explicitly rejected in FEAT-0015.
- **Why rejected**: Contradicts the primary user value proposition of the extension.

### Option C: VS SDK `ProvideService` / `ProvideAutoLoad` with a custom service contract

- **Pros**: Leverages VS extensibility model; no JSON file dependency.
- **Cons**: Requires defining a custom VS service contract and a corresponding VS SDK client inside VS 2025's MCP
  host — not feasible for a third-party extension; VS 2025's MCP host has no such extension point.
- **Why rejected**: Extension point does not exist in VS 2025's MCP subsystem.

## Consequences

### Positive

- Zero-friction registration: the developer does not need to edit any JSON file manually.
- Self-healing: the entry is refreshed on every VS startup, keeping the binary path current after extension upgrades.
- Consistent pattern with VS Code (FEAT-0015), which writes `mcpServers` configuration to VS Code settings.
- VS 2025 manages the full process lifecycle (start, stop, crash recovery), removing that responsibility from the
  extension.

### Negative / Trade-offs

- Writing to `%APPDATA%\Microsoft\VisualStudio\mcp.json` on every package load requires careful JSON merge logic
  to avoid corrupting other MCP server entries the user may have added manually.
- The Token Secret is stored as a plain environment variable value in `mcp.json`, which is readable by the current
  Windows user. This is equivalent to the VS Code settings.json approach (ADR-0011). Migration to Windows
  Credential Manager is deferred to v2.
- If VS 2025 changes the location or schema of `mcp.json` in a future version, the extension will need a
  corresponding update.

## Related ADRs

- [ADR-0009: Dual-Transport Entry-Point Strategy](ADR-0009-dual-transport-entry-point.md)
- [ADR-0011: VS Code Extension Binary Bundling Strategy](ADR-0011-vscode-extension-binary-bundling.md)
- [ADR-0018: VSIX Extension Project Layout](ADR-0018-vsix-project-layout.md)
