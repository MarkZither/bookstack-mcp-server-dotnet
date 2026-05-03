# Implementation Plan: Visual Studio 2025 Extension Packaging

**Feature**: FEAT-0019
**Spec**: [docs/features/visual-studio-extension/spec.md](spec.md)
**GitHub Issue**: [#19](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/19)
**Status**: Ready for implementation

---

## Architecture Decisions

| ADR | Title | Decision Summary |
|-----|-------|-----------------|
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution Structure | `visual-studio-extension/` at repo root; separate `.sln` from root solution (see ADR-0018) |
| [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) | CI/CD | GitHub Actions; release.yml extended with `windows-latest` job for VSIX build + publish |
| [ADR-0009](../../architecture/decisions/ADR-0009-dual-transport-entry-point.md) | Dual-Transport | stdio transport only for v1; VS 2025 manages the server process |
| [ADR-0011](../../architecture/decisions/ADR-0011-vscode-extension-binary-bundling.md) | Binary Bundling | win-x64 self-contained single-file binary bundled inside the VSIX |
| [ADR-0017](../../architecture/decisions/ADR-0017-vs2025-mcp-registration.md) | VS 2025 MCP Registration | Write server entry to `%APPDATA%\Microsoft\VisualStudio\mcp.json` on package load |
| [ADR-0018](../../architecture/decisions/ADR-0018-vsix-project-layout.md) | VSIX Project Layout | `visual-studio-extension/` at root level; separate solution; `net10.0-windows` TFM |

---

## File Layout

```
bookstack-mcp-server-dotnet/
  visual-studio-extension/
    BookStack.Mcp.VsExtension.csproj    # VSIX SDK project targeting net10.0-windows
    BookStack.Mcp.VsExtension.sln       # Separate solution (includes server project ref)
    source.extension.vsixmanifest        # Extension manifest (minimum VS version 18.0)
    BookStackMcpPackage.cs               # AsyncPackage entry point
    Options/
      BookStackOptionsPage.cs            # DialogPage — Tools > Options page
    Services/
      McpRegistrationService.cs          # Writes/updates mcp.json entry
    Resources/
      icon.png                           # 128×128 px Marketplace icon
    bin/                                 # CI-populated; gitignored
      BookStack.Mcp.Server.exe           # win-x64 self-contained binary
    README.md                            # VS Marketplace listing README
    CHANGELOG.md                         # Keep-a-Changelog format
  src/
    BookStack.Mcp.Server/                # Existing server project — no changes
  .github/
    workflows/
      release.yml                        # Extended: VSIX build + Marketplace publish step
```

`visual-studio-extension/bin/` is listed in `.gitignore`. Binaries are never committed; they are created by CI.

---

## Dependencies and Toolchain

| Dependency | Version | Purpose |
|------------|---------|---------|
| `Microsoft.VisualStudio.SDK` | ≥ 18.0.x | VS 2025 AsyncPackage, `DialogPage`, `IVsOutputWindow` |
| `Microsoft.VSSDK.BuildTools` | ≥ 18.0.x | VSIX packaging (`CreateVsixContainer` build target) |
| `Microsoft.VisualStudio.Threading.Analyzers` | Latest | Async/await correctness analyzers for VS packages |
| `System.Text.Json` | (via .NET 10 BCL) | `mcp.json` read/write |
| `dotnet build` | .NET 10 SDK | Build and package VSIX on `windows-latest` CI runner |
| `vsix` CLI / `VsixPublisher.exe` | via `vsce`-equivalent for VS | Publish to Visual Studio Marketplace |

CI builds the VSIX on a `windows-latest` GitHub Actions runner. The `windows-latest` runner is required because
`net10.0-windows` projects cannot be built on Linux, and VSIX packaging (`CreateVsixContainer`) requires Windows.

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

---

### Task 1 — Bootstrap `visual-studio-extension/` project skeleton

**Goal**: Create the project file, solution file, and extension manifest so the project builds (empty package) on
a `windows-latest` runner.

**Files to create or update**:

- `visual-studio-extension/BookStack.Mcp.VsExtension.csproj` — VSIX SDK project; `net10.0-windows`; NuGet refs to
  `Microsoft.VisualStudio.SDK` and `Microsoft.VSSDK.BuildTools`.
- `visual-studio-extension/source.extension.vsixmanifest` — product name, publisher `MarkZither`, minimum VS
  version `18.0`, asset entry for the binary under `Assets`.
- `visual-studio-extension/BookStack.Mcp.VsExtension.sln` — solution containing the VSIX project and a
  `ProjectReference` to `../src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj`.
- `visual-studio-extension/Resources/icon.png` — placeholder 128×128 px icon.
- `visual-studio-extension/CHANGELOG.md` — initial entry for 0.1.0.
- `.gitignore` update — add `visual-studio-extension/bin/` exclusion.

**Acceptance**: `dotnet build visual-studio-extension/BookStack.Mcp.VsExtension.csproj` on `windows-latest`
produces a `.vsix` file with no build errors.

---

### Task 2 — Implement `BookStackMcpPackage` AsyncPackage

**Goal**: Create the package entry point that loads on VS startup and wires up the options page.

**Files to create**:

- `visual-studio-extension/BookStackMcpPackage.cs` — class inheriting `AsyncPackage`; `[PackageRegistration]`,
  `[Guid]`, `[ProvideOptionPage]`, `[ProvideAutoLoad(UIContextGuids80.SolutionExists)]` attributes; override
  `InitializeAsync` to load options and call `McpRegistrationService`.

**Acceptance**: Extension loads in an Experimental VS Instance (F5) without errors; "BookStack MCP Server" page
appears under Tools > Options > BookStack.

---

### Task 3 — Implement `BookStackOptionsPage` Tools > Options page

**Goal**: Expose three labelled settings (BookStack URL, Token ID, Token Secret) through the VS Tools > Options
dialog using `DialogPage`.

**Files to create**:

- `visual-studio-extension/Options/BookStackOptionsPage.cs` — class inheriting `DialogPage`; three `string`
  properties: `BookStackUrl`, `TokenId`, `TokenSecret`; all three annotated with `[Category("BookStack MCP Server")]`
  and `[DisplayName]` / `[Description]` attributes.

**Implementation notes**:

- `DialogPage` persists settings to the VS private registry automatically — no explicit `SaveSettingsToStorage`
  call needed for basic string properties.
- `TokenSecret` is stored as plaintext in VS's private registry (DPAPI-encrypted by VS on Windows). Masking (custom
  `UITypeEditor`) is deferred to v2 (OQ-4 in spec).

**Acceptance**: Three labelled fields are visible in Tools > Options; values survive VS restart.

---

### Task 4 — Implement `McpRegistrationService`

**Goal**: Read the options and write (or update) the server entry in `%APPDATA%\Microsoft\VisualStudio\mcp.json`.
Remove the entry on package disposal.

**Files to create**:

- `visual-studio-extension/Services/McpRegistrationService.cs`:
  - `RegisterAsync(string bookStackUrl, string tokenId, string tokenSecret, string binaryPath)` — validates that
    all three setting values are non-empty; if any is blank, writes a message to the VS Output window and returns
    without modifying `mcp.json`.
  - JSON merge logic: read existing `mcp.json` (or create if absent), add/replace the `"bookstack"` key under
    `"servers"`, write back. Uses `System.Text.Json` with `JsonSerializerOptions.WriteIndented = true`.
  - `UnregisterAsync()` — removes the `"bookstack"` key from `mcp.json` on extension unload/uninstall.
  - Binary path resolved via `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)`.

**Security rules** (enforced in this class):
- `TokenSecret` value is **never** passed to `ILogger`, `IVsOutputWindowPane`, or any diagnostic channel.
- `BOOKSTACK_TOKEN_SECRET` env var value is assembled as `$"{tokenId}:{tokenSecret}"` inline and written directly
  to the JSON file without intermediate logging.

**Acceptance**:
- Given all three settings are configured, `%APPDATA%\Microsoft\VisualStudio\mcp.json` contains a `"bookstack"`
  entry with correct `command` path and `env` values after VS loads the extension.
- Given any setting is blank, no `mcp.json` write occurs; VS Output window shows a configuration warning.
- Given VS is closed, the `mcp.json` entry persists (VS 2025 manages the server process; the entry is not cleaned
  up on normal VS close, only on extension uninstall).

---

### Task 5 — Bundle win-x64 binary in VSIX

**Goal**: Configure the VSIX project so that the win-x64 server binary is included in the `.vsix` package at the
`bin/BookStack.Mcp.Server.exe` path.

**Files to update**:

- `visual-studio-extension/source.extension.vsixmanifest` — add `<Asset>` element:
  ```xml
  <Asset Type="Microsoft.VisualStudio.MefComponent" d:Source="File"
         Path="bin\BookStack.Mcp.Server.exe" />
  ```
- `visual-studio-extension/BookStack.Mcp.VsExtension.csproj` — add `<Content>` item for
  `bin\BookStack.Mcp.Server.exe` with `<IncludeInVSIX>true</IncludeInVSIX>`.

**CI note**: The binary is not present in the repository; `visual-studio-extension/bin/` is gitignored. CI must
build the win-x64 server binary and copy it to `visual-studio-extension/bin/` before running `dotnet build` on the
VSIX project (see Task 7).

**Acceptance**: The `.vsix` produced by `dotnet build` contains `bin/BookStack.Mcp.Server.exe` and the file is
accessible at `Path.Combine(extensionInstallDir, "bin", "BookStack.Mcp.Server.exe")` at runtime.

---

### Task 6 — Marketplace README and documentation

**Goal**: Produce the three documentation artefacts specified in the spec's Documentation Requirements section.

**Files to create**:

- `visual-studio-extension/README.md` — Marketplace listing README covering: what the extension does; prerequisites
  (BookStack instance + API token; no .NET SDK required); quick-start steps; configuration reference table;
  supported platforms (Windows x64 only); link to demo instance; link to GitHub issues.
- `visual-studio-extension/CHANGELOG.md` — initial entry for 0.1.0 (already created in Task 1; update with final
  release date).
- `docs/features/visual-studio-extension/` (this folder) — local development guide covering the PowerShell
  commands from the spec's Documentation Requirements §2.

**Acceptance**: `README.md` and `CHANGELOG.md` present in `visual-studio-extension/`; quick-start steps in README
verified against a working extension install.

---

### Task 7 — CI release workflow additions

**Goal**: Extend `.github/workflows/release.yml` to build the win-x64 binary, assemble the VSIX, and publish to
the Visual Studio Marketplace when a `v*.*.*` tag is pushed.

**Changes to `.github/workflows/release.yml`**:

1. **`build-vs-extension` job** (new, `runs-on: windows-latest`, `needs: build-binaries`):
   - Download the `binary-win-x64` artifact (built by the existing `build-binaries` job).
   - Copy `BookStack.Mcp.Server.exe` to `visual-studio-extension/bin/`.
   - Verify SHA-256 hash of the binary against a signed manifest (supply-chain check per spec NFR).
   - Run `dotnet build visual-studio-extension/BookStack.Mcp.VsExtension.csproj -c Release` to produce the `.vsix`.
   - Sign the VSIX with Authenticode (certificate stored in `VS_SIGNING_CERT` secret; signing strategy TBD — OQ-2
     in spec; placeholder step added, certificate procurement deferred).
   - Upload the `.vsix` as a GitHub Actions artifact (`vsix-package`).

2. **`publish-vs-marketplace` job** (new, `runs-on: windows-latest`, `needs: build-vs-extension`):
   - Download the `vsix-package` artifact.
   - Attach the `.vsix` to the GitHub Release (using the existing `softprops/action-gh-release` step, updated to
     include the VSIX alongside the existing assets).
   - Publish to the Visual Studio Marketplace using `VsixPublisher.exe` or the `marketplace-publish` GitHub Action,
     authenticated with `VS_MARKETPLACE_PAT` secret.

**Security**:
- `VS_MARKETPLACE_PAT` and `VS_SIGNING_CERT` are repository secrets; never echoed to logs.
- All third-party Actions pinned to commit SHA (per ADR-0003).
- Job-level `permissions: contents: write` scoped to the release job only.

**Acceptance**: Given a `v*.*.*` tag push, the CI pipeline produces a `.vsix` attached to the GitHub Release and
the extension version on the Visual Studio Marketplace matches the tag.

---

### Task 8 — Manual acceptance testing

**Goal**: Verify all acceptance criteria from the spec against the F5-deployed Experimental VS Instance and a
sideloaded VSIX before moving the spec to `Approved`.

**Test checklist** (from spec Acceptance Criteria):

- [ ] Install VSIX (or F5 Experimental Instance); set URL + Token ID + Token Secret in Tools > Options;
  verify `mcp.json` entry written; verify `initialize` handshake within 5 seconds.
- [ ] Leave any setting blank; verify VS Output window message; verify no `mcp.json` entry written.
- [ ] With all settings present, start VS; confirm `BookStack.Mcp.Server.exe` is running as a child of
  `devenv.exe` within 5 seconds.
- [ ] Use demo instance (`https://demo.bookstackapp.com/`) with a valid token; confirm Copilot Chat `books_list`
  tool returns a non-empty list.
- [ ] Close VS normally; confirm `BookStack.Mcp.Server.exe` is not left as an orphan.
- [ ] Build VSIX via CI on a `v*.*.*` tag; confirm `.vsix` attached to GitHub Release; confirm Marketplace version
  updated.
- [ ] Measure VSIX file size; confirm under 70 MB.
- [ ] Inspect VS Output window and `ActivityLog.xml` with Token Secret configured; confirm Token Secret does not
  appear.
- [ ] Open Tools > Options; confirm "BookStack MCP Server" page under "BookStack" category with three fields.

---

## Security Notes

- `TokenSecret` is never logged at any verbosity level — enforced in `McpRegistrationService` (Task 4).
- `mcp.json` contains `BOOKSTACK_TOKEN_SECRET` as a plain env var value, readable by the current Windows user.
  Equivalent to the VS Code settings.json approach (ADR-0011). Credential Manager migration is deferred to v2.
- Win-x64 binary SHA-256 hash is verified by CI before VSIX assembly (supply-chain check).
- VSIX must be Authenticode-signed before Marketplace submission; certificate procurement is tracked as OQ-2 in the
  spec and is a prerequisite for the first Marketplace publish.
- No outbound network calls from the extension itself — all network I/O is the server process's responsibility.
- Input values from the Options page are never included in telemetry, even in debug builds.

---

## Open Questions (tracked in spec)

| ID | Question | Blocking? |
|----|----------|-----------|
| OQ-2 | Authenticode certificate procurement and CI signing strategy | Yes — blocks first Marketplace publish |
| OQ-4 | Feasibility of masked `TokenSecret` field via custom `UITypeEditor` | No — deferred to v2 |

---

## Commands

Executable commands for this project (copy and run directly):

### Build (VSIX — Windows only)

```powershell
dotnet build visual-studio-extension/BookStack.Mcp.VsExtension.csproj -c Release
```

### Build server binary for local testing

```powershell
dotnet publish src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -o visual-studio-extension/bin/
```

### Build (server — cross-platform)

```
dotnet build BookStack.Mcp.Server.sln --configuration Release
```

### Tests

```
dotnet test --configuration Release
```

### Lint / Formatting

```
dotnet format --verify-no-changes
```

### Local Extension Testing (F5 Experimental Instance)

Open `visual-studio-extension/BookStack.Mcp.VsExtension.sln` in Visual Studio 2025. Set
`BookStack.Mcp.VsExtension` as the startup project. Press F5 to launch an Experimental VS Instance with the
extension loaded.

### Verify server binary standalone (PowerShell)

```powershell
$env:BOOKSTACK_BASE_URL = "https://demo.bookstackapp.com/"
$env:BOOKSTACK_TOKEN_SECRET = "tokenId:tokenSecret"
$init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":' +
        '{"protocolVersion":"2024-11-05","capabilities":{},' +
        '"clientInfo":{"name":"test","version":"0.0.1"}}}'
echo $init | .\visual-studio-extension\bin\BookStack.Mcp.Server.exe
```

Expected: JSON-RPC `initialize` response on stdout; no errors on stderr.
