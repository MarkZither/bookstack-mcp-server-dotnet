import { vi } from 'vitest';

export const workspace = {
    getConfiguration: vi.fn(() => ({
        get: vi.fn((_key: string, defaultVal: unknown) => defaultVal),
    })),
};

export const window = {
    createStatusBarItem: vi.fn(() => ({
        command: undefined,
        text: '',
        tooltip: '',
        show: vi.fn(),
        dispose: vi.fn(),
    })),
    showWarningMessage: vi.fn(),
    showErrorMessage: vi.fn(),
    createOutputChannel: vi.fn(() => ({
        appendLine: vi.fn(),
        dispose: vi.fn(),
    })),
    createWebviewPanel: vi.fn(),
};

export const StatusBarAlignment = { Right: 1, Left: 0 };

export const ViewColumn = { Beside: -2, Active: -1, One: 1 };

export const commands = {
    registerCommand: vi.fn(),
    executeCommand: vi.fn(),
};

export const lm = {
    registerMcpServerDefinitionProvider: vi.fn(),
};
