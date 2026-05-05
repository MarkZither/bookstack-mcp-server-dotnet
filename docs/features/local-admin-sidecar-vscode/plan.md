# Plan: VS Code Extension — Admin Status Bar & WebviewPanel (FEAT-0056)

**Feature**: VS Code Extension — Admin Status Bar & WebviewPanel
**Spec**: [docs/features/local-admin-sidecar-vscode/spec.md](spec.md)
**GitHub Issue**: [#80](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/80)
**Status**: Ready for Implementation
**Date**: 2026-05-05

---

## Referenced ADRs / Specs

| Reference | Title | Relevance |
|-----------|-------|-----------|
| [FEAT-0055](../local-admin-sidecar/spec.md) | Local Admin HTTP Sidecar | Provides `GET /admin/status`, `POST /admin/sync`, `POST /admin/index` — the endpoints this feature polls and calls |
| [FEAT-0015](../scaffold-and-cicd/spec.md) | VS Code Extension Packaging | Extension distribution channel; esbuild single-file bundle; `package.json` `contributes` schema |

No ADRs are required for this feature. All technology choices (Node built-in `fetch`, esbuild, TypeScript module splitting) follow patterns already established in the extension and do not introduce architectural decisions at the system level.

---

## Data Model Changes

None. This feature is entirely within the VS Code extension host (TypeScript). No .NET entities, database migrations, or `IVectorStore` changes are required.

---

## Configuration Changes

Add `bookstack.adminPort` to `contributes.configuration.properties` in `vscode-extension/package.json`:

```json
"bookstack.adminPort": {
  "type": "number",
  "default": 5174,
  "minimum": 1,
  "maximum": 65535,
  "markdownDescription": "Port of the local admin sidecar. Must match `BOOKSTACK_ADMIN_PORT` set on the MCP server process. Admin features are unavailable when the sidecar is not running."
}
```

---

## Component Diagram

```mermaid
graph TD
    subgraph VS Code Extension Host
        A[extension.ts<br/>activate] --> B[StatusBarManager]
        A --> C[AdminPanelProvider]
        A --> D[AdminSidecarClient]
        B -->|poll every 30 s<br/>setTimeout + backoff| D
        C -->|Sync Now| D
        C -->|Index Page| D
        B -->|state update| C
        D -->|AdminStatus| B
        D -->|AdminStatus| C
    end

    subgraph WebviewPanel
        G[stats table]
        H[Sync Now button]
        I[URL input + Index Page]
        G & H & I -->|postMessage| C
        C -->|postMessage| G & H & I
    end

    D -->|fetch GET /admin/status<br/>5 s timeout| J[Admin Sidecar<br/>localhost:{adminPort}<br/>FEAT-0055]
    D -->|fetch POST /admin/sync| J
    D -->|fetch POST /admin/index| J
```

---

## Implementation Approach

### File Structure

Split the current single-file `extension.ts` by introducing three focused modules alongside it. esbuild bundles all TypeScript source files into a single `dist/extension.js` output, so module boundaries have no runtime cost.

```
vscode-extension/src/
  extension.ts              ← existing; updated in Phase 4
  adminSidecarClient.ts     ← new Phase 1
  statusBarManager.ts       ← new Phase 2
  adminPanelProvider.ts     ← new Phase 3
```

---

### Phase 1 — `adminSidecarClient.ts` — typed HTTP wrapper

**Files created**: `vscode-extension/src/adminSidecarClient.ts`

Provides typed `fetch`-based calls to the three admin endpoints. All HTTP is performed from the extension host; the webview never calls the sidecar directly.

`fetch` is available as a Node.js built-in from Node 18+, which VS Code 1.99 provides. No additional dependencies are required.

```typescript
export interface AdminStatus {
    totalPages: number;
    lastSyncTime: string | null;
    pendingCount: number;
}

export interface AdminAccepted {
    status: 'accepted';
}

export interface AdminError {
    error: string;
}

export type AdminResult<T> =
    | { ok: true; data: T }
    | { ok: false; kind: 'unreachable' | 'error'; message: string };

export class AdminSidecarClient {
    private readonly timeoutMs = 5_000;

    constructor(private readonly getPort: () => number) {}

    async getStatus(): Promise<AdminResult<AdminStatus>> {
        return this.request<AdminStatus>('GET', '/admin/status');
    }

    async postSync(): Promise<AdminResult<AdminAccepted>> {
        return this.request<AdminAccepted>('POST', '/admin/sync');
    }

    async postIndex(url: string): Promise<AdminResult<AdminAccepted>> {
        return this.request<AdminAccepted>('POST', '/admin/index', { url });
    }

    private async request<T>(
        method: string,
        path: string,
        body?: unknown
    ): Promise<AdminResult<T>> {
        const port = this.getPort();
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), this.timeoutMs);
        try {
            const response = await fetch(`http://127.0.0.1:${port}${path}`, {
                method,
                headers: body ? { 'Content-Type': 'application/json' } : undefined,
                body: body ? JSON.stringify(body) : undefined,
                signal: controller.signal,
            });
            if (!response.ok) {
                return { ok: false, kind: 'unreachable', message: `HTTP ${response.status}` };
            }
            let data: T;
            try {
                data = (await response.json()) as T;
            } catch {
                return { ok: false, kind: 'error', message: 'Malformed JSON response from admin sidecar.' };
            }
            return { ok: true, data };
        } catch (err: unknown) {
            const isTimeout = err instanceof Error && err.name === 'AbortError';
            return {
                ok: false,
                kind: 'unreachable',
                message: isTimeout ? 'Request timed out.' : String(err),
            };
        } finally {
            clearTimeout(timeout);
        }
    }
}

export function resolveAdminPort(): number {
    // Import vscode lazily to keep this module testable without the VS Code host.
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const vscode = require('vscode') as typeof import('vscode');
    const config = vscode.workspace.getConfiguration('bookstack');
    const raw = config.get<number>('adminPort', 5174);
    if (!Number.isInteger(raw) || raw < 1 || raw > 65535) {
        vscode.window.showWarningMessage(
            `BookStack: bookstack.adminPort value "${raw}" is invalid. Falling back to default port 5174.`
        );
        return 5174;
    }
    return raw;
}
```

---

### Phase 2 — `statusBarManager.ts` — status bar + polling loop

**Files created**: `vscode-extension/src/statusBarManager.ts`

Manages the status bar item lifecycle and the polling loop with exponential backoff. Implements `vscode.Disposable` so it integrates cleanly with `context.subscriptions`.

```typescript
import * as vscode from 'vscode';
import { AdminSidecarClient, AdminStatus } from './adminSidecarClient';

export type StatusBarState =
    | { kind: 'idle'; totalPages: number }
    | { kind: 'syncing' }
    | { kind: 'unreachable' }
    | { kind: 'error' };

export class StatusBarManager implements vscode.Disposable {
    private readonly item: vscode.StatusBarItem;
    private readonly minInterval = 30_000;
    private readonly maxInterval = 300_000;
    private interval = 30_000;
    currentState: StatusBarState = { kind: 'unreachable' };
    latestStatus: AdminStatus | null = null;
    private timer: ReturnType<typeof setTimeout> | null = null;
    private disposed = false;

    onStateChange?: (state: StatusBarState, status: AdminStatus | null) => void;

    constructor(
        private readonly client: AdminSidecarClient,
        private readonly onClickCommand: string,
    ) {
        this.item = vscode.window.createStatusBarItem(
            vscode.StatusBarAlignment.Right,
            100
        );
        this.item.command = onClickCommand;
        this.item.show();
        this.renderState({ kind: 'unreachable' });
        this.scheduleNext(0); // poll immediately on activation
    }

    /** Call after triggering a sync to switch to the syncing state immediately. */
    setSyncing(): void {
        this.applyState({ kind: 'syncing' });
    }

    private scheduleNext(delay: number): void {
        if (this.disposed) { return; }
        this.timer = setTimeout(() => void this.poll(), delay);
    }

    private async poll(): Promise<void> {
        if (this.disposed) { return; }
        const result = await this.client.getStatus();
        if (this.disposed) { return; }

        if (result.ok) {
            this.interval = this.minInterval; // reset on success
            this.latestStatus = result.data;
            const newState: StatusBarState = { kind: 'idle', totalPages: result.data.totalPages };
            this.applyState(newState);
        } else if (result.kind === 'error') {
            this.latestStatus = null;
            this.applyState({ kind: 'error' });
            this.interval = Math.min(this.interval * 2, this.maxInterval);
        } else {
            this.latestStatus = null;
            this.applyState({ kind: 'unreachable' });
            this.interval = Math.min(this.interval * 2, this.maxInterval);
        }

        this.scheduleNext(this.interval);
    }

    private applyState(state: StatusBarState): void {
        this.currentState = state;
        this.renderState(state);
        this.onStateChange?.(state, this.latestStatus);
    }

    private renderState(state: StatusBarState): void {
        switch (state.kind) {
            case 'idle':
                this.item.text = `$(database) BookStack: ${state.totalPages} pages`;
                this.item.tooltip = 'Click to open BookStack Admin panel';
                break;
            case 'syncing':
                this.item.text = '$(sync~spin) BookStack: syncing\u2026';
                this.item.tooltip = 'Vector index sync in progress';
                break;
            case 'unreachable':
                this.item.text = '$(database) BookStack: N/A';
                this.item.tooltip = 'Admin sidecar unreachable — check bookstack.adminPort and that the MCP server is running';
                break;
            case 'error':
                this.item.text = '$(warning) BookStack: error';
                this.item.tooltip = 'Admin sidecar returned an unexpected response';
                break;
        }
    }

    dispose(): void {
        this.disposed = true;
        if (this.timer !== null) {
            clearTimeout(this.timer);
            this.timer = null;
        }
        this.item.dispose();
    }
}
```

**Key design choices**:

- `setTimeout`-based recursion (not `setInterval`) so the backoff interval can be applied cleanly after each result.
- `scheduleNext(0)` on construction triggers an immediate first poll so the status bar shows a real value within milliseconds of activation rather than waiting 30 seconds.
- `disposed` guard in `scheduleNext` and `poll` prevents ghost timers after deactivation.

---

### Phase 3 — `adminPanelProvider.ts` — WebviewPanel with CSP

**Files created**: `vscode-extension/src/adminPanelProvider.ts`

Manages the singleton WebviewPanel. Receives state updates from `StatusBarManager` via the `onStateChange` callback and forwards `postMessage` events from the webview to `AdminSidecarClient`.

```typescript
import * as vscode from 'vscode';
import * as crypto from 'crypto';
import { AdminSidecarClient, AdminStatus } from './adminSidecarClient';
import { StatusBarManager } from './statusBarManager';

export class AdminPanelProvider implements vscode.Disposable {
    private panel: vscode.WebviewPanel | undefined;

    constructor(
        private readonly client: AdminSidecarClient,
        private readonly statusBarManager: StatusBarManager,
        private readonly context: vscode.ExtensionContext,
    ) {}

    /** Open the panel or focus it if already open. */
    openOrFocus(latestStatus: AdminStatus | null, unreachable: boolean): void {
        if (this.panel) {
            this.panel.reveal(vscode.ViewColumn.Beside);
            return;
        }
        const panel = vscode.window.createWebviewPanel(
            'bookstackAdmin',
            'BookStack Admin',
            vscode.ViewColumn.Beside,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [],
            }
        );
        this.panel = panel;
        this.renderHtml(latestStatus, unreachable);

        panel.webview.onDidReceiveMessage(
            (msg: { command: string; url?: string }) => {
                void this.handleMessage(msg);
            },
            undefined,
            this.context.subscriptions
        );

        panel.onDidDispose(() => {
            this.panel = undefined;
        }, undefined, this.context.subscriptions);
    }

    /** Called by StatusBarManager.onStateChange to push fresh data to an open panel. */
    updateStats(status: AdminStatus | null, unreachable: boolean): void {
        if (!this.panel) { return; }
        if (status) {
            void this.panel.webview.postMessage({
                command: 'updateStats',
                totalPages: status.totalPages,
                lastSyncTime: status.lastSyncTime,
                pendingCount: status.pendingCount,
            });
        } else {
            void this.panel.webview.postMessage({ command: 'sidecarUnreachable' });
        }
        // Re-render HTML to reflect current reachability (enables/disables controls)
        this.renderHtml(status, unreachable);
    }

    private async handleMessage(msg: { command: string; url?: string }): Promise<void> {
        if (msg.command === 'syncNow') {
            this.statusBarManager.setSyncing();
            const result = await this.client.postSync();
            if (result.ok) {
                void this.panel?.webview.postMessage({ command: 'syncStarted' });
            } else {
                void this.panel?.webview.postMessage({ command: 'syncError', message: result.message });
            }
        } else if (msg.command === 'indexPage') {
            const url = msg.url?.trim() ?? '';
            if (!url) {
                void this.panel?.webview.postMessage({ command: 'indexError', message: 'Error: URL is required.' });
                return;
            }
            const result = await this.client.postIndex(url);
            if (result.ok) {
                void this.panel?.webview.postMessage({ command: 'indexStarted' });
            } else {
                void this.panel?.webview.postMessage({ command: 'indexError', message: `Error: ${result.message}` });
            }
        }
    }

    private renderHtml(status: AdminStatus | null, unreachable: boolean): void {
        if (!this.panel) { return; }
        const nonce = crypto.randomUUID().replace(/-/g, '');
        this.panel.webview.html = buildWebviewHtml(nonce, status, unreachable);
    }

    dispose(): void {
        this.panel?.dispose();
        this.panel = undefined;
    }
}

function buildWebviewHtml(nonce: string, status: AdminStatus | null, unreachable: boolean): string {
    const lastSync = status?.lastSyncTime
        ? new Date(status.lastSyncTime).toUTCString()
        : '\u2014';
    const statsHtml = unreachable || !status
        ? `<p class="warning">&#9888; Admin sidecar unreachable. Check that the MCP server is running and <code>bookstack.adminPort</code> is correct.</p>`
        : `<table>
             <tr><th>Total Pages</th><td id="totalPages">${status.totalPages}</td></tr>
             <tr><th>Last Sync</th><td id="lastSync">${lastSync}</td></tr>
             <tr><th>Pending</th><td id="pending">${status.pendingCount}</td></tr>
           </table>`;
    const controlsDisabled = unreachable || !status ? 'disabled' : '';

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>BookStack Admin</title>
  <style>
    body { font-family: var(--vscode-font-family); color: var(--vscode-foreground); padding: 16px; }
    table { border-collapse: collapse; width: 100%; margin-bottom: 16px; }
    th, td { padding: 6px 10px; border: 1px solid var(--vscode-panel-border); text-align: left; }
    button { padding: 6px 12px; margin-right: 8px; cursor: pointer; }
    button:disabled { opacity: 0.5; cursor: not-allowed; }
    input[type=text] { width: 60%; padding: 4px 8px; }
    #status { margin-top: 12px; font-style: italic; }
    .warning { color: var(--vscode-errorForeground); }
  </style>
</head>
<body>
  <h2>BookStack Admin</h2>
  ${statsHtml}
  <div>
    <button id="syncBtn" ${controlsDisabled}>Sync Now</button>
  </div>
  <br>
  <div>
    <label for="pageUrl">Page URL:</label><br>
    <input type="text" id="pageUrl"
           placeholder="https://bookstack.example.com/books/b/pages/p"
           ${controlsDisabled}>
    <button id="indexBtn" ${controlsDisabled}>Index Page</button>
  </div>
  <p id="status"></p>
  <script nonce="${nonce}">
    const vscode = acquireVsCodeApi();
    document.getElementById('syncBtn').addEventListener('click', () => {
      vscode.postMessage({ command: 'syncNow' });
    });
    document.getElementById('indexBtn').addEventListener('click', () => {
      const url = document.getElementById('pageUrl').value;
      vscode.postMessage({ command: 'indexPage', url });
    });
    window.addEventListener('message', event => {
      const msg = event.data;
      const statusEl = document.getElementById('status');
      if (msg.command === 'updateStats') {
        const totalEl = document.getElementById('totalPages');
        const syncEl  = document.getElementById('lastSync');
        const pendEl  = document.getElementById('pending');
        if (totalEl) { totalEl.textContent = String(msg.totalPages); }
        if (syncEl)  { syncEl.textContent  = msg.lastSyncTime
            ? new Date(msg.lastSyncTime).toUTCString() : '\u2014'; }
        if (pendEl)  { pendEl.textContent  = String(msg.pendingCount); }
      } else if (msg.command === 'syncStarted') {
        statusEl.textContent = 'Sync started';
      } else if (msg.command === 'syncError') {
        statusEl.textContent = 'Sync error: ' + msg.message;
      } else if (msg.command === 'indexStarted') {
        statusEl.textContent = 'Indexing started';
      } else if (msg.command === 'indexError') {
        statusEl.textContent = msg.message;
      } else if (msg.command === 'sidecarUnreachable') {
        statusEl.textContent = 'Admin sidecar unreachable.';
      }
    });
  </script>
</body>
</html>`;
}
```

**CSP design**:

- `default-src 'none'` blocks all external network requests from the webview.
- `script-src 'nonce-{nonce}'` allows only the inline script tagged with the matching nonce. The nonce is regenerated on every `renderHtml` call via `crypto.randomUUID()`.
- `style-src 'unsafe-inline'` is required for VS Code CSS custom properties (`var(--vscode-*)`); no external stylesheets are loaded.
- No credentials, API tokens, or BookStack settings values appear in any `postMessage` payload.

---

### Phase 4 — `extension.ts` and `package.json` changes

**Files changed**: `vscode-extension/src/extension.ts`, `vscode-extension/package.json`

Wire up the three new modules in `activate` and register the `bookstack.openAdminPanel` command.

#### `package.json` — add command contribution

Add to `contributes`:

```json
"commands": [
  {
    "command": "bookstack.openAdminPanel",
    "title": "BookStack: Open Admin Panel"
  }
]
```

Also add `bookstack.adminPort` to `contributes.configuration.properties` as described in the Configuration Changes section above.

#### `extension.ts` — additions to `activate`

Import the new modules at the top of the file:

```typescript
import { AdminSidecarClient, resolveAdminPort } from './adminSidecarClient';
import { StatusBarManager } from './statusBarManager';
import { AdminPanelProvider } from './adminPanelProvider';
```

Add the following block inside `activate`, after the existing MCP provider registration succeeds:

```typescript
// Admin sidecar status bar and panel
const adminClient = new AdminSidecarClient(resolveAdminPort);

const statusBarManager = new StatusBarManager(adminClient, 'bookstack.openAdminPanel');

const adminPanelProvider = new AdminPanelProvider(adminClient, statusBarManager, context);

statusBarManager.onStateChange = (state, latestStatus) => {
    const unreachable = state.kind === 'unreachable';
    adminPanelProvider.updateStats(latestStatus, unreachable);
};

const openPanelCmd = vscode.commands.registerCommand('bookstack.openAdminPanel', () => {
    const unreachable = statusBarManager.currentState.kind === 'unreachable';
    adminPanelProvider.openOrFocus(statusBarManager.latestStatus, unreachable);
});

context.subscriptions.push(statusBarManager, adminPanelProvider, openPanelCmd);
```

> The `AdminSidecarClient` is constructed with `resolveAdminPort` as the `getPort` callback (not a captured value), so configuration changes take effect on the next poll without requiring an extension restart.

---

### Phase 5 — Unit tests

**Files created**: `vscode-extension/tests/adminSidecarClient.test.ts`, `vscode-extension/tests/statusBarManager.test.ts`

The extension has no existing test infrastructure. Tests use Node's built-in `node:test` runner and `assert` module to avoid adding test-framework dependencies. `fetch` is mocked by replacing the global.

#### `adminSidecarClient.test.ts`

```typescript
import { describe, it, before, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { AdminSidecarClient } from '../src/adminSidecarClient';

describe('AdminSidecarClient.getStatus', () => {
    let originalFetch: typeof globalThis.fetch;
    before(() => { originalFetch = globalThis.fetch; });
    afterEach(() => { globalThis.fetch = originalFetch; });

    it('returns ok:true with parsed data on 200 response', async () => {
        globalThis.fetch = async () => new Response(
            JSON.stringify({ totalPages: 42, lastSyncTime: null, pendingCount: 0 }),
            { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
        const client = new AdminSidecarClient(() => 5174);
        const result = await client.getStatus();
        assert.equal(result.ok, true);
        if (result.ok) { assert.equal(result.data.totalPages, 42); }
    });

    it('returns ok:false kind:unreachable on non-2xx response', async () => {
        globalThis.fetch = async () => new Response('', { status: 503 });
        const client = new AdminSidecarClient(() => 5174);
        const result = await client.getStatus();
        assert.equal(result.ok, false);
        if (!result.ok) { assert.equal(result.kind, 'unreachable'); }
    });

    it('returns ok:false kind:error on malformed JSON', async () => {
        globalThis.fetch = async () => new Response('not-json', { status: 200 });
        const client = new AdminSidecarClient(() => 5174);
        const result = await client.getStatus();
        assert.equal(result.ok, false);
        if (!result.ok) { assert.equal(result.kind, 'error'); }
    });

    it('returns ok:false kind:unreachable on fetch rejection (network error)', async () => {
        globalThis.fetch = async () => { throw new Error('ECONNREFUSED'); };
        const client = new AdminSidecarClient(() => 5174);
        const result = await client.getStatus();
        assert.equal(result.ok, false);
        if (!result.ok) { assert.equal(result.kind, 'unreachable'); }
    });

    it('uses the port returned by getPort on each call', async () => {
        let capturedPort: number | undefined;
        globalThis.fetch = async (input: RequestInfo | URL) => {
            capturedPort = parseInt(new URL(input as string).port, 10);
            return new Response(
                JSON.stringify({ totalPages: 0, lastSyncTime: null, pendingCount: 0 }),
                { status: 200 }
            );
        };
        const client = new AdminSidecarClient(() => 9999);
        await client.getStatus();
        assert.equal(capturedPort, 9999);
    });
});
```

#### `statusBarManager.test.ts` — backoff logic (pure helper)

```typescript
import { describe, it } from 'node:test';
import assert from 'node:assert/strict';

// The backoff computation is extracted as a pure helper to keep tests
// free of VS Code API mocks. The implementation worker may expose this
// helper from statusBarManager.ts or test it via a full vscode mock.

function computeNextInterval(success: boolean, current: number): number {
    const min = 30_000;
    const max = 300_000;
    return success ? min : Math.min(current * 2, max);
}

describe('StatusBarManager backoff logic', () => {
    it('resets interval to 30 000 on success', () => {
        assert.equal(computeNextInterval(true, 120_000), 30_000);
    });

    it('doubles interval on failure', () => {
        assert.equal(computeNextInterval(false, 30_000), 60_000);
    });

    it('caps interval at 300 000', () => {
        assert.equal(computeNextInterval(false, 150_000), 300_000);
        assert.equal(computeNextInterval(false, 300_000), 300_000);
    });
});
```

---

## Test Plan

| Test | Acceptance Criterion | Mechanism |
|------|---------------------|-----------|
| `getStatus` 200 → `ok:true` with correct `totalPages` | FR-3, NFR-2 | Unit: mock `fetch` |
| `getStatus` 503 → `ok:false kind:unreachable` | FR-5 | Unit: mock `fetch` |
| `getStatus` malformed JSON → `ok:false kind:error` | FR-6 | Unit: mock `fetch` |
| Network error / `ECONNREFUSED` → `ok:false kind:unreachable` | FR-5, NFR-2 | Unit: mock `fetch` throws |
| `getPort()` callback used on each call | FR-1, FR-7 | Unit: capture URL in mock `fetch` |
| Backoff resets to 30 s on success | FR-8 | Unit: pure helper |
| Backoff doubles on failure, caps at 300 s | FR-8 | Unit: pure helper |
| Status bar shows `$(database) BookStack: N pages` | FR-3 | Manual smoke test |
| Status bar shows `$(sync~spin) BookStack: syncing…` after Sync Now | FR-4, FR-13 | Manual smoke test |
| Status bar shows `$(database) BookStack: N/A` when sidecar down | FR-5 | Manual smoke test (stop sidecar) |
| Status bar shows `$(warning) BookStack: error` on bad JSON | FR-6 | Manual smoke test |
| Click status bar → opens WebviewPanel | FR-9 | Manual smoke test |
| Second click → focuses existing panel, no duplicate opened | FR-9 | Manual smoke test |
| WebviewPanel stats table populated from poll data | FR-10, FR-15 | Manual smoke test |
| Sync Now → `POST /admin/sync` called; panel shows `Sync started` | FR-11, FR-13 | Manual smoke test |
| Index Page with valid URL → `POST /admin/index` called; panel shows `Indexing started` | FR-12, FR-14 | Manual smoke test |
| Index Page with empty URL → panel shows error; no HTTP request sent | FR-14 | Manual smoke test |
| Sidecar unreachable → panel shows warning; controls disabled | FR-16 | Manual smoke test |
| Deactivate extension → polling stops; status bar disposed | FR-17 | Manual smoke test |
| `bookstack.adminPort` invalid value → VS Code warning; falls back to 5174 | Security | Manual smoke test |

---

## Security Notes

- **CSP nonce**: The WebviewPanel HTML is regenerated on every `renderHtml` call with a fresh nonce from `crypto.randomUUID()`. The CSP is `default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-{nonce}'`. No `unsafe-inline` for scripts is permitted.
- **No external network from webview**: `default-src 'none'` blocks all outbound requests from the webview. All HTTP calls to the admin sidecar are made exclusively from the extension host.
- **No credentials in postMessage**: The `postMessage` protocol carries only index stats (`totalPages`, `lastSyncTime`, `pendingCount`) and operation results. No BookStack API token, token secret, `bookstack.url`, or any other credential appears in any webview message payload in either direction.
- **Port validation**: `resolveAdminPort()` rejects non-integer values and values outside 1–65535, surfaces a VS Code warning, and falls back to port 5174. This prevents malformed configuration from causing unexpected network connections.
- **Localhost only**: `AdminSidecarClient` always targets `http://127.0.0.1:{port}`. The port comes from VS Code configuration, never from a user-supplied field in the webview, so it cannot be redirected to an arbitrary host.
- **No authentication in Phase 1**: The admin sidecar carries no authentication (FEAT-0055 Phase 1). Extension documentation must note that the admin port should only be used in trusted local development environments.

---

## Out of Scope

- Kestrel second-listener setup, REST endpoint contracts, or `BOOKSTACK_ADMIN_PORT` env var (FEAT-0055).
- Pre-shared token authentication for the admin sidecar (future phase aligned with FEAT-0055 roadmap).
- Auto-opening the WebviewPanel on activation when the sidecar is already reachable (explicit click only — auto-open is disruptive).
- Dedicated output channel or progress notification for sync operations.
- macOS / non-Linux platform support in this phase (the bundled binary is Linux-only in the current release).

---

## Commands

### Build

```bash
cd vscode-extension && npm run build
```

### Watch (incremental rebuild)

```bash
cd vscode-extension && npm run watch
```

### Lint

```bash
cd vscode-extension && npm run lint
```

### Unit Tests

```bash
cd vscode-extension && node --test tests/adminSidecarClient.test.ts tests/statusBarManager.test.ts
```

### Package extension

```bash
cd vscode-extension && npm run package
```

### Sideload for manual smoke testing

```powershell
# Windows (PowerShell)
cd vscode-extension && .\sideload.ps1
```
