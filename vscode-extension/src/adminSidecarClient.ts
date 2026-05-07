export interface AdminStatus {
    totalPages: number;
    lastSyncTime: string | null;
    pendingCount: number;
}

export interface AdminAccepted {
    status: 'accepted';
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
                // 5xx = server is reachable but erroring; 4xx non-404 = unexpected
                // Treat 404/connection-refused as unreachable, server errors as 'error'
                const kind = response.status >= 500 ? 'error' : 'unreachable';
                return { ok: false, kind, message: `HTTP ${response.status}` };
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

type ConfigGetter = { get: <T>(key: string, defaultValue: T) => T };
type WarnFn = (message: string) => void;

export function resolveAdminPort(
    configGetter?: ConfigGetter,
    warnFn?: WarnFn
): number {
    if (!configGetter) {
        // Import vscode lazily to keep this module testable without the VS Code host.
        // eslint-disable-next-line @typescript-eslint/no-require-imports
        const vscode = require('vscode') as typeof import('vscode');
        configGetter = vscode.workspace.getConfiguration('bookstack');
        warnFn = (msg) => { void vscode.window.showWarningMessage(msg); };
    }
    const warn = warnFn ?? (() => undefined);
    const raw = configGetter.get<number>('adminPort', 5174);
    if (!Number.isInteger(raw) || raw < 1 || raw > 65535) {
        warn(`BookStack: bookstack.adminPort value "${raw}" is invalid. Falling back to default port 5174.`);
        return 5174;
    }
    return raw;
}
