# Tasks: VS Code Extension — Admin Status Bar & WebviewPanel (FEAT-0056)

**Feature**: VS Code Extension — Admin Status Bar & WebviewPanel
**Spec**: [docs/features/local-admin-sidecar-vscode/spec.md](spec.md)
**Plan**: [docs/features/local-admin-sidecar-vscode/plan.md](plan.md)
**GitHub Issue**: [#80](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/80)
**Status**: Decomposed
**Date**: 2026-05-05

---

## Phase 1 — AdminSidecarClient

- [ ] Create `AdminSidecarClient` with typed `fetch` wrapper (`GET /admin/status`, `POST /admin/sync`, `POST /admin/index`), `AdminResult<T>` discriminated union, and 5 s `AbortController` timeout in `vscode-extension/src/adminSidecarClient.ts`
- [ ] Implement `resolveAdminPort()` helper that reads `bookstack.adminPort` config, validates integer range 1–65535, warns and falls back to `5174` on invalid value in `vscode-extension/src/adminSidecarClient.ts`

## Phase 2 — StatusBarManager

- [ ] Create `StatusBarManager` (implements `vscode.Disposable`) with `setTimeout`-recursion polling loop, exponential backoff (30 s initial, doubles on failure, max 300 s, resets to 30 s on success), and four status bar states (idle, syncing, unreachable, error) in `vscode-extension/src/statusBarManager.ts`
- [ ] Expose `setSyncing()` method on `StatusBarManager` so callers can immediately switch to syncing state after triggering a sync (before the next poll confirms it) in `vscode-extension/src/statusBarManager.ts`
- [ ] Expose `onStateChange` callback so `AdminPanelProvider` can receive state updates and refresh the WebviewPanel in `vscode-extension/src/statusBarManager.ts`

## Phase 3 — AdminPanelProvider (WebviewPanel)

- [ ] Create `AdminPanelProvider` that manages a singleton `vscode.WebviewPanel` (focus existing if already open, create otherwise), generates a per-load CSP nonce via `crypto.randomUUID()`, and renders inline HTML with stats table, Sync Now button, and Page URL + Index Page controls in `vscode-extension/src/adminPanelProvider.ts`
- [ ] Implement webview → extension host message handler: `syncNow` calls `client.postSync()` + `statusBar.setSyncing()` + sends `syncStarted` or `syncError` back; `indexPage` validates URL is non-empty, calls `client.postIndex(url)`, sends `indexStarted` or `indexError` back in `vscode-extension/src/adminPanelProvider.ts`
- [ ] Implement `updateStats(status, state)` method that posts `updateStats` message to webview when panel is visible; call from `StatusBarManager.onStateChange` in `vscode-extension/src/adminPanelProvider.ts`
- [ ] Disable Sync Now and Index Page controls (and show unreachable warning) when `sidecarUnreachable` message is posted to webview in `vscode-extension/src/adminPanelProvider.ts`

## Phase 4 — Extension wiring & package.json

- [ ] Add `bookstack.adminPort` setting (`number`, default `5174`, minimum `1`, maximum `65535`) to `contributes.configuration.properties` in `vscode-extension/package.json`
- [ ] Register `bookstack.openAdminPanel` command in `contributes.commands` in `vscode-extension/package.json`
- [ ] Update `vscode-extension/src/extension.ts`: import and instantiate `AdminSidecarClient`, `StatusBarManager`, `AdminPanelProvider`; register `bookstack.openAdminPanel` command handler; push all disposables to `context.subscriptions`

## Phase 5 — Unit tests

- [ ] Add `AdminSidecarClient` unit tests with mock `fetch`: `getStatus_success_returnsOkWithData`, `getStatus_timeout_returnsUnreachable`, `getStatus_nonOkStatus_returnsUnreachable`, `getStatus_malformedJson_returnsError` in `vscode-extension/src/adminSidecarClient.test.ts`
- [ ] Add `resolveAdminPort` unit tests: `resolveAdminPort_validPort_returnsPort`, `resolveAdminPort_outOfRange_returnsDefaultAndWarns`, `resolveAdminPort_nonInteger_returnsDefaultAndWarns` in `vscode-extension/src/adminSidecarClient.test.ts`
- [ ] Add `StatusBarManager` backoff unit tests (pure helper, no VS Code host needed): `backoff_firstFailure_doubles`, `backoff_capsAtMaxInterval`, `backoff_successResetsInterval` in `vscode-extension/src/statusBarManager.test.ts`
