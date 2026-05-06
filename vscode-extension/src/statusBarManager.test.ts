import { describe, it, expect, vi, beforeEach } from 'vitest';
import { StatusBarManager } from './statusBarManager';
import type { AdminSidecarClient } from './adminSidecarClient';

// The vitest.config.ts aliases 'vscode' to the __mocks__/vscode.ts stub.

function makeClient(
    overrides: Partial<AdminSidecarClient> = {}
): AdminSidecarClient {
    return {
        getStatus: vi.fn().mockResolvedValue({ ok: true, data: { totalPages: 1, lastSyncTime: null, pendingCount: 0 } }),
        postSync: vi.fn(),
        postIndex: vi.fn(),
        ...overrides,
    } as unknown as AdminSidecarClient;
}

describe('StatusBarManager backoff', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    it('backoff_firstFailure_doubles', async () => {
        const client = makeClient({
            getStatus: vi.fn().mockResolvedValue({ ok: false, kind: 'unreachable', message: 'down' }),
        });
        const mgr = new StatusBarManager(client, 'bookstack.openAdminPanel');
        const initial = mgr.interval;

        // Run the immediate poll scheduled at construction.
        await mgr.poll();

        expect(mgr.interval).toBe(Math.min(initial * 2, mgr.maxInterval));
        mgr.dispose();
    });

    it('backoff_capsAtMaxInterval', async () => {
        const client = makeClient({
            getStatus: vi.fn().mockResolvedValue({ ok: false, kind: 'unreachable', message: 'down' }),
        });
        const mgr = new StatusBarManager(client, 'bookstack.openAdminPanel');
        mgr.interval = mgr.maxInterval; // force to max already

        await mgr.poll();

        expect(mgr.interval).toBe(mgr.maxInterval);
        mgr.dispose();
    });

    it('backoff_successResetsInterval', async () => {
        const client = makeClient({
            getStatus: vi.fn().mockResolvedValue({ ok: true, data: { totalPages: 5, lastSyncTime: null, pendingCount: 0 } }),
        });
        const mgr = new StatusBarManager(client, 'bookstack.openAdminPanel');
        mgr.interval = mgr.maxInterval; // pretend we had repeated failures

        await mgr.poll();

        expect(mgr.interval).toBe(mgr.minInterval);
        mgr.dispose();
    });
});
