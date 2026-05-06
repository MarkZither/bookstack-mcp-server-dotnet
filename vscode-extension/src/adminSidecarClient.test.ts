import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { AdminSidecarClient, resolveAdminPort } from './adminSidecarClient';

describe('AdminSidecarClient', () => {
    const port = 5174;
    let client: AdminSidecarClient;

    beforeEach(() => {
        client = new AdminSidecarClient(() => port);
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('getStatus_success_returnsOkWithData', async () => {
        const payload = { totalPages: 42, lastSyncTime: '2026-01-01T00:00:00Z', pendingCount: 0 };
        global.fetch = vi.fn().mockResolvedValueOnce({
            ok: true,
            json: async () => payload,
        } as unknown as Response);

        const result = await client.getStatus();

        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(result.data.totalPages).toBe(42);
        }
    });

    it('getStatus_nonOkStatus_returnsUnreachable', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce({
            ok: false,
            status: 404,
        } as unknown as Response);

        const result = await client.getStatus();

        expect(result.ok).toBe(false);
        if (!result.ok) {
            expect(result.kind).toBe('unreachable');
            expect(result.message).toContain('404');
        }
    });

    it('getStatus_timeout_returnsUnreachable', async () => {
        global.fetch = vi.fn().mockImplementationOnce(
            (_url: string, opts: { signal: AbortSignal }) =>
                new Promise((_resolve, reject) => {
                    opts.signal.addEventListener('abort', () => {
                        const err = new Error('AbortError');
                        err.name = 'AbortError';
                        reject(err);
                    });
                })
        );

        // Use a client with a very short timeout via direct private access workaround —
        // instead, just confirm that when AbortError is thrown the result is unreachable.
        // We trigger manually by aborting immediately.
        const result = await (async () => {
            const abortErr = new Error('AbortError');
            abortErr.name = 'AbortError';
            global.fetch = vi.fn().mockRejectedValueOnce(abortErr);
            return client.getStatus();
        })();

        expect(result.ok).toBe(false);
        if (!result.ok) {
            expect(result.kind).toBe('unreachable');
            expect(result.message).toBe('Request timed out.');
        }
    });

    it('getStatus_malformedJson_returnsError', async () => {
        global.fetch = vi.fn().mockResolvedValueOnce({
            ok: true,
            json: async () => { throw new SyntaxError('bad json'); },
        } as unknown as Response);

        const result = await client.getStatus();

        expect(result.ok).toBe(false);
        if (!result.ok) {
            expect(result.kind).toBe('error');
            expect(result.message).toContain('Malformed JSON');
        }
    });
});

describe('resolveAdminPort', () => {
    it('resolveAdminPort_validPort_returnsPort', () => {
        const configGetter = { get: (_key: string, _def: unknown) => 8080 };

        const result = resolveAdminPort(configGetter as never);

        expect(result).toBe(8080);
    });

    it('resolveAdminPort_outOfRange_returnsDefaultAndWarns', () => {
        const configGetter = { get: (_key: string, _def: unknown) => 99999 };
        const warn = vi.fn();

        const result = resolveAdminPort(configGetter as never, warn);

        expect(result).toBe(5174);
        expect(warn).toHaveBeenCalled();
    });

    it('resolveAdminPort_nonInteger_returnsDefaultAndWarns', () => {
        const configGetter = { get: (_key: string, _def: unknown) => 'not-a-number' };
        const warn = vi.fn();

        const result = resolveAdminPort(configGetter as never, warn);

        expect(result).toBe(5174);
        expect(warn).toHaveBeenCalled();
    });
});
