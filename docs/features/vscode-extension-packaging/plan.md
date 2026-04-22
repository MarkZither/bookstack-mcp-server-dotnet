# Implementation Plan: VS Code Extension Packaging

**Feature**: FEAT-0015
**Spec**: [docs/features/vscode-extension-packaging/spec.md](spec.md)
**GitHub Issue**: [#15](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15)
**Status**: Ready for implementation

---

## Architecture Decisions

| ADR | Title | Decision Summary |
|-----|-------|-----------------|
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution Structure | New `vscode-extension/` project at repo root alongside `src/` |
| [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) | CI/CD | GitHub Actions; new `release.yml` extends existing pipeline |
| [ADR-0009](../../architecture/decisions/ADR-0009-dual-transport-entry-point.md) | Dual-Transport | stdio transport used by the extension; server binary started as a child process |
| [ADR-0011](../../architecture/decisions/ADR-0011-vscode-extension-binary-bundling.md) | Binary Bundling | Self-contained single-file binaries for win-x64 + linux-x64 bundled inside the VSIX |

---

## File Layout

```
bookstack-mcp-server-dotnet/
  vscode-extension/
    package.json              # Extension manifest — contributes mcpServers + settings
    package-lock.json         # Committed lockfile (npm ci in CI)
    tsconfig.json             # TypeScript compiler config
    .eslintrc.json            # ESLint config
    .vscodeignore             # Controls what is excluded from the VSIX
    .vscode/
      launch.json             # F5 Extension Development Host configuration
      tasks.json              # Pre-launch build task
    src/
      extension.ts            # Sole TypeScript source file — activation + process mgmt
    media/
      icon.png                # Marketplace icon (128×128 px minimum, PNG)
    bin/                      # Created at CI time — gitignored; holds platform binaries
      bookstack-mcp-server.exe        # win-x64 binary (CI-built)
      bookstack-mcp-server-linux      # linux-x64 binary (CI-built)
    README.md                 # Marketplace listing README
    CHANGELOG.md              # Keep-a-Changelog format
  src/
    BookStack.Mcp.Server/     # Existing — no changes to project file needed for bundling
  .github/
    workflows/
      ci.yml                  # Extended: add extension lint step
      release.yml             # New: build binaries + package + publish VSIX
```

`vscode-extension/bin/` is listed in `.gitignore` — binaries are never committed; they are created by CI.

---

## Dependencies and Toolchain

| Tool | Version | Purpose |
|------|---------|---------|
| Node.js | 22 LTS | Extension build runtime |
| TypeScript | ~5.7 | Extension source language |
| `@vscode/vsce` | ^3 | VSIX packaging and Marketplace publish |
| `esbuild` | ^0.25 | TypeScript bundling (faster than webpack; no config overhead) |
| `@types/vscode` | ^1.95.0 | VS Code API types |
| `@typescript-eslint/parser` | ^8 | Linting |
| `eslint` | ^9 | Linting |

`esbuild` is used in place of webpack for simplicity. The single entry point `src/extension.ts` produces `dist/extension.js`.

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

---

### Task 1 — Bootstrap `vscode-extension/` package

**Goal**: Create the Node/TypeScript project skeleton so subsequent tasks have something to build on.

**Files to create**:

`vscode-extension/package.json`:
```jsonc
{
  "name": "bookstack-mcp-server",
  "displayName": "BookStack MCP Server",
  "description": "MCP server for BookStack — gives AI assistants (GitHub Copilot, Claude) access to your BookStack knowledge base.",
  "version": "0.1.0",
  "publisher": "MarkZither",
  "engines": { "vscode": "^1.95.0" },
  "extensionKind": ["workspace"],
  "categories": ["AI", "Other"],
  "keywords": ["bookstack", "mcp", "copilot", "knowledge-base"],
  "license": "MIT",
  "repository": { "type": "git", "url": "https://github.com/MarkZither/bookstack-mcp-server-dotnet" },
  "icon": "media/icon.png",
  "main": "./dist/extension.js",
  "activationEvents": ["onStartupFinished"],
  "contributes": {
    "mcpServers": {
      "bookstack": {
        "command": "${extensionPath}/bin/bookstack-mcp-server-linux",
        "args": [],
        "env": {
          "BOOKSTACK_BASE_URL": "${config:bookstack.url}",
          "BOOKSTACK_TOKEN_SECRET": "${config:bookstack.tokenSecret}"
        }
      }
    },
    "configuration": {
      "title": "BookStack MCP Server",
      "properties": {
        "bookstack.url": {
          "type": "string",
          "default": "",
          "markdownDescription": "URL of your BookStack instance. Example: `https://demo.bookstackapp.com/`"
        },
        "bookstack.tokenId": {
          "type": "string",
          "default": "",
          "markdownDescription": "BookStack API token ID. Generate under BookStack → **Settings → API Tokens**."
        },
        "bookstack.tokenSecret": {
          "type": "string",
          "default": "",
          "markdownDescription": "BookStack API token secret for the token ID above."
        }
      }
    }
  },
  "scripts": {
    "build": "esbuild src/extension.ts --bundle --platform=node --external:vscode --outfile=dist/extension.js",
    "watch": "esbuild src/extension.ts --bundle --platform=node --external:vscode --outfile=dist/extension.js --watch",
    "lint": "eslint src",
    "package": "vsce package"
  },
  "devDependencies": {
    "@types/vscode": "^1.95.0",
    "@typescript-eslint/eslint-plugin": "^8",
    "@typescript-eslint/parser": "^8",
    "esbuild": "^0.25",
    "eslint": "^9",
    "typescript": "~5.7",
    "@vscode/vsce": "^3"
  }
}
```

> **Note on `mcpServers` command**: VS Code resolves the `command` field at runtime. The static
> placeholder above will be overridden by `extension.ts` at activation time — see Task 2.
> The static field only serves as a fallback / documentation; the real binary path is injected
> programmatically.

`vscode-extension/tsconfig.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "lib": ["ES2022"],
    "outDir": "dist",
    "rootDir": "src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true
  },
  "include": ["src"]
}
```

`vscode-extension/.vscodeignore`:
```
.vscode/**
src/**
node_modules/**
*.ts
tsconfig.json
.eslintrc.json
esbuild.js
```

`vscode-extension/bin/.gitkeep` (empty file so the directory is tracked; actual binaries are gitignored):

Add to root `.gitignore`:
```
vscode-extension/bin/bookstack-mcp-server.exe
vscode-extension/bin/bookstack-mcp-server-linux
vscode-extension/dist/
vscode-extension/node_modules/
```

**Acceptance**: `npm ci && npm run build` succeeds in `vscode-extension/`.

---

### Task 2 — Implement `extension.ts`

**Goal**: Activation logic — validate settings, resolve platform binary, inject token env var, surface error for unsupported platforms.

**File**: `vscode-extension/src/extension.ts`

```typescript
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export function activate(context: vscode.ExtensionContext): void {
    const config = vscode.workspace.getConfiguration('bookstack');
    const url = config.get<string>('url', '').trim();
    const tokenId = config.get<string>('tokenId', '').trim();
    const tokenSecret = config.get<string>('tokenSecret', '').trim();

    if (!url || !tokenId || !tokenSecret) {
        vscode.window.showWarningMessage(
            'BookStack MCP Server: URL, Token ID, and Token Secret are all required.',
            'Open Settings'
        ).then(selection => {
            if (selection === 'Open Settings') {
                vscode.commands.executeCommand(
                    'workbench.action.openSettings',
                    'bookstack'
                );
            }
        });
        return;
    }

    const binaryName = resolveBinaryName();
    if (!binaryName) {
        vscode.window.showErrorMessage(
            `BookStack MCP Server: platform '${process.platform}' is not supported in this release. ` +
            'See https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15 for status.'
        );
        return;
    }

    const binaryPath = path.join(context.extensionPath, 'bin', binaryName);
    if (!fs.existsSync(binaryPath)) {
        vscode.window.showErrorMessage(
            `BookStack MCP Server: bundled binary not found at '${binaryPath}'. ` +
            'Try reinstalling the extension.'
        );
        return;
    }

    // MCP server registration is handled declaratively via the mcpServers contribution.
    // extension.ts only needs to validate settings and surface errors; VS Code's MCP host
    // reads the contribution point and spawns the process itself.
    // Future: use vscode.lm.registerMcpServer() API if programmatic override is needed.

    const outputChannel = vscode.window.createOutputChannel('BookStack MCP Server');
    context.subscriptions.push(outputChannel);
    outputChannel.appendLine(`BookStack MCP Server: activating with URL ${url}`);
    outputChannel.appendLine(`BookStack MCP Server: binary path ${binaryPath}`);
}

export function deactivate(): void {
    // VS Code manages the MCP server process lifecycle; no cleanup required here.
}

function resolveBinaryName(): string | undefined {
    switch (process.platform) {
        case 'win32':  return 'bookstack-mcp-server.exe';
        case 'linux':  return 'bookstack-mcp-server-linux';
        default:       return undefined;
    }
}
```

**Design note**: VS Code's `mcpServers` contribution point spawns the server process automatically
using the `command` declared in `package.json`. The token concatenation (`tokenId:tokenSecret`)
cannot be expressed purely in the `package.json` env-var template syntax; the workaround is to
have `extension.ts` write a transient environment variable or use VS Code's settings substitution.

**Known limitation for Task 2**: The `mcpServers` `env` field in `package.json` does not support
expressions like `${config:bookstack.tokenId}:${config:bookstack.tokenSecret}`. The recommended
approach is to store the combined value in a single `bookstack.tokenSecret` setting that the user
populates as `tokenId:tokenSecret`, **or** to use the VS Code extension host's
`vscode.workspace.getConfiguration` to assemble the combined value and write it to an intermediate
setting or a server-managed env var.

**Resolution**: `extension.ts` will use `vscode.lm.registerMcpServer()` (if available in the
target VS Code version) or will write the combined token to a workspace-scoped setting
`bookstack._combinedToken` (hidden, prefixed with `_` to signal it is internal) that the
`mcpServers` contribution reads via `${config:bookstack._combinedToken}`. This keeps the user
experience clean (two separate fields) while satisfying the contribution point constraint.

**Acceptance**: Extension Development Host (`F5`) activates without errors when all three settings
are set; shows warning notification when any setting is blank.

---

### Task 3 — Add `.vscode/launch.json` and `tasks.json` for F5 debugging

**Goal**: Developers can press F5 in `vscode-extension/` to launch the Extension Development Host with the TypeScript debugger attached.

**File**: `vscode-extension/.vscode/launch.json`

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Run Extension",
      "type": "extensionHost",
      "request": "launch",
      "args": ["--extensionDevelopmentPath=${workspaceFolder}"],
      "outFiles": ["${workspaceFolder}/dist/**/*.js"],
      "preLaunchTask": "npm: build"
    }
  ]
}
```

**File**: `vscode-extension/.vscode/tasks.json`

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "type": "npm",
      "script": "build",
      "group": "build",
      "problemMatcher": ["$tsc"],
      "label": "npm: build",
      "detail": "esbuild bundle"
    }
  ]
}
```

**Acceptance**: F5 opens Extension Development Host; breakpoints in `extension.ts` are hit on activation.

---

### Task 4 — Add ESLint configuration

**Goal**: `npm run lint` enforces consistent TypeScript style.

**File**: `vscode-extension/.eslintrc.json`

```json
{
  "root": true,
  "parser": "@typescript-eslint/parser",
  "parserOptions": { "ecmaVersion": 2022, "sourceType": "module" },
  "plugins": ["@typescript-eslint"],
  "rules": {
    "@typescript-eslint/no-unused-vars": "error",
    "@typescript-eslint/explicit-function-return-type": "warn",
    "no-console": "error"
  }
}
```

**Acceptance**: `npm run lint` exits 0 with no warnings on the initial `extension.ts`.

---

### Task 5 — Add `dotnet publish` profiles for win-x64 and linux-x64

**Goal**: Enable `dotnet publish` to produce single-file self-contained binaries for each target platform from `ubuntu-latest`.

Add a `<PublishProfile>` or use directory publish profiles. The simplest approach is to document the publish command directly in the release workflow (Task 7) rather than creating `.pubxml` files, since the flags are straightforward.

Verify the publish command works locally:

```bash
cd src/BookStack.Mcp.Server

# linux-x64
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=embedded \
  -o ../../vscode-extension/bin/linux-x64/

# win-x64
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=embedded \
  -o ../../vscode-extension/bin/win-x64/
```

Then rename/copy to the expected flat layout:
```bash
cp vscode-extension/bin/linux-x64/BookStack.Mcp.Server  vscode-extension/bin/bookstack-mcp-server-linux
cp vscode-extension/bin/win-x64/BookStack.Mcp.Server.exe  vscode-extension/bin/bookstack-mcp-server.exe
chmod +x vscode-extension/bin/bookstack-mcp-server-linux
```

**Acceptance**: Both binaries exist in `vscode-extension/bin/`; `./bin/bookstack-mcp-server-linux --version` (or equivalent smoke test) exits without error.

---

### Task 6 — Add marketplace assets

**Goal**: The VSIX passes Marketplace validation (icon, README, CHANGELOG required).

**Files to create**:

- `vscode-extension/media/icon.png` — 128×128 px PNG. Placeholder acceptable for initial `.vsix`; final version before Marketplace publish.
- `vscode-extension/README.md` — Marketplace listing README per spec Documentation Requirements §1.
- `vscode-extension/CHANGELOG.md` — Keep-a-Changelog format per spec §4.

**README.md must cover** (per spec):
- One-paragraph description
- Prerequisites (no .NET required; BookStack instance + API token)
- Quick-start (5 steps: install → open settings → enter URL + tokenId + tokenSecret → open Copilot Chat → verify with `@bookstack list all books`)
- Demo instance note: `https://demo.bookstackapp.com/`
- Configuration reference table
- Supported platforms: win-x64, linux-x64 (macOS: not yet supported)
- Link to GitHub Issues for bug reports

**Acceptance**: `npx vsce package` completes without warnings about missing icon/README/CHANGELOG.

---

### Task 7 — Create `.github/workflows/release.yml`

**Goal**: On push of a `v*.*.*` tag, build both platform binaries, package the VSIX, attach it to a GitHub Release, and publish to the VS Code Marketplace.

**File**: `.github/workflows/release.yml`

```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'

permissions:
  contents: write   # needed to create GitHub Release

jobs:
  build-binaries:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        include:
          - rid: linux-x64
            output: bookstack-mcp-server-linux
          - rid: win-x64
            output: bookstack-mcp-server.exe
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Publish ${{ matrix.rid }}
        run: |
          dotnet publish src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:DebugType=embedded \
            -o publish/${{ matrix.rid }}/

      - name: Stage binary
        run: |
          mkdir -p vscode-extension/bin
          cp publish/${{ matrix.rid }}/BookStack.Mcp.Server* vscode-extension/bin/${{ matrix.output }}
          chmod +x vscode-extension/bin/${{ matrix.output }} || true

      - uses: actions/upload-artifact@v4
        with:
          name: binary-${{ matrix.rid }}
          path: vscode-extension/bin/${{ matrix.output }}

  package-and-publish:
    runs-on: ubuntu-latest
    needs: build-binaries
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: '22'

      - uses: actions/download-artifact@v4
        with:
          name: binary-linux-x64
          path: vscode-extension/bin/

      - uses: actions/download-artifact@v4
        with:
          name: binary-win-x64
          path: vscode-extension/bin/

      - name: Make linux binary executable
        run: chmod +x vscode-extension/bin/bookstack-mcp-server-linux

      - name: npm ci
        working-directory: vscode-extension
        run: npm ci

      - name: Build extension
        working-directory: vscode-extension
        run: npm run build

      - name: Package VSIX
        working-directory: vscode-extension
        run: npx vsce package --out bookstack-mcp-server.vsix

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: vscode-extension/bookstack-mcp-server.vsix

      - name: Publish to Marketplace
        working-directory: vscode-extension
        env:
          VSCE_PAT: ${{ secrets.VSCE_PAT }}
        run: npx vsce publish --pat ${{ env.VSCE_PAT }}
```

**Required GitHub secret**: `VSCE_PAT` — a Personal Access Token for the VS Code Marketplace
(generated at https://marketplace.visualstudio.com/manage under the `MarkZither` publisher).
This must be added to the repository's Actions secrets before the first release.

**Acceptance**: Pushing `git tag v0.1.0 && git push origin v0.1.0` triggers the workflow; VSIX is attached to the GitHub Release.

---

### Task 8 — Extend `ci.yml` with extension lint step

**Goal**: PRs are blocked if the extension TypeScript has lint errors.

Add a new job to `.github/workflows/ci.yml`:

```yaml
  extension-lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: '22'

      - name: npm ci
        working-directory: vscode-extension
        run: npm ci

      - name: Lint
        working-directory: vscode-extension
        run: npm run lint
```

**Acceptance**: CI passes on main with the new job; a deliberate lint error in a PR causes CI to fail.

---

## Token Concatenation — Implementation Note

The `mcpServers` contribution point's `env` field only supports static `${config:setting}` substitution — it cannot concatenate two settings. The implementation approach is:

1. `extension.ts` reads `bookstack.tokenId` and `bookstack.tokenSecret` separately.
2. It writes the combined value `tokenId:tokenSecret` to a hidden internal setting `bookstack._token` using `vscode.workspace.getConfiguration('bookstack').update('_token', combined, vscode.ConfigurationTarget.Global)`.
3. `package.json` `mcpServers.env.BOOKSTACK_TOKEN_SECRET` references `${config:bookstack._token}`.

Alternatively, if `vscode.lm.registerMcpServer()` is available and stable in VS Code 1.95+, use the programmatic API to register the server with the full env object constructed in `extension.ts` — this is cleaner and avoids the hidden setting. Research which API is available at the target version before implementing.

---

## Test Strategy

There is no automated test framework for VS Code extensions in this project (TUnit is .NET-only). Testing is manual:

| Test | Method |
|------|--------|
| F5 activation with all settings set | Extension Development Host — verify Output channel message and no error notification |
| F5 activation with blank settings | Verify warning notification + "Open Settings" button appears |
| F5 activation on unsupported platform | Not directly testable on linux/win CI; unit-test `resolveBinaryName()` via a small Jest/Vitest test if desired |
| VSIX sideload on Windows | Manual — install `.vsix`, configure settings, verify `tools/list` in Copilot Chat |
| VSIX sideload on Linux | Manual — same as above |
| Release workflow | Trigger with a test tag `v0.0.1-test`; verify artifact attached to GitHub Release |

---

## Pre-Implementation Prerequisites

- [ ] **Marketplace publisher account**: Verify `MarkZither` publisher exists at https://marketplace.visualstudio.com/manage. If not, create it before Task 7 is implemented.
- [ ] **`VSCE_PAT` GitHub secret**: Generate a PAT scoped to Marketplace publish and add it to the repository's Actions secrets (`Settings → Secrets → Actions`).
- [ ] **`vscode-extension/bin/` gitignored**: Confirm root `.gitignore` excludes the platform binaries before any binary is accidentally committed.
