import * as vscode from 'vscode';
import { AdminSidecarClient, AdminStatus } from './adminSidecarClient';

export type StatusBarState =
    | { kind: 'idle'; totalPages: number }
    | { kind: 'syncing' }
    | { kind: 'unreachable' }
    | { kind: 'error' };

export class StatusBarManager implements vscode.Disposable {
    private readonly item: vscode.StatusBarItem;
    readonly minInterval = 30_000;
    readonly maxInterval = 300_000;
    interval = 30_000;
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

    scheduleNext(delay: number): void {
        if (this.disposed) { return; }
        this.timer = setTimeout(() => void this.poll(), delay);
    }

    async poll(): Promise<void> {
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
                this.item.tooltip = 'Admin sidecar unreachable \u2014 check bookstack.adminPort and that the MCP server is running';
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
