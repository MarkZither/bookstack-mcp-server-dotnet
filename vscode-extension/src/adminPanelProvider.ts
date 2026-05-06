import * as vscode from 'vscode';
import * as crypto from 'crypto';
import { AdminSidecarClient, AdminStatus } from './adminSidecarClient';
import { StatusBarManager, StatusBarState } from './statusBarManager';

export class AdminPanelProvider implements vscode.Disposable {
    private panel: vscode.WebviewPanel | undefined;

    constructor(
        private readonly client: AdminSidecarClient,
        private readonly statusBarManager: StatusBarManager,
        private readonly context: vscode.ExtensionContext,
    ) {}

    /** Open the panel or focus it if already open. */
    openOrFocus(): void {
        const status = this.statusBarManager.latestStatus;
        const unreachable = this.statusBarManager.currentState.kind === 'unreachable';
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
        this.renderHtml(status, unreachable);

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
    handleStateChange(state: StatusBarState, status: AdminStatus | null): void {
        if (!this.panel) { return; }
        const unreachable = state.kind === 'unreachable';
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
