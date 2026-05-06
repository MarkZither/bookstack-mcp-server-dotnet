import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AdminSidecarClient, resolveAdminPort } from './adminSidecarClient';
import { StatusBarManager } from './statusBarManager';
import { AdminPanelProvider } from './adminPanelProvider';

export function activate(context: vscode.ExtensionContext): void {
    const outputChannel = vscode.window.createOutputChannel('BookStack MCP Server');
    context.subscriptions.push(outputChannel);

    outputChannel.appendLine('BookStack MCP Server: activating...');

    outputChannel.appendLine(`  platform : ${process.platform}`);
    outputChannel.appendLine(`  extension: ${context.extensionPath}`);

    const binaryName = resolveBinaryName();
    outputChannel.appendLine(`  binary name resolved: ${binaryName ?? '(unsupported platform)'}`);
    if (!binaryName) {
        const msg = `BookStack MCP Server: platform '${process.platform}' is not supported in this release. ` +
            'See https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15 for status.';
        vscode.window.showErrorMessage(msg);
        outputChannel.appendLine(msg);
        return;
    }

    const binaryPath = path.join(context.extensionPath, 'bin', binaryName);
    outputChannel.appendLine(`  binary path: ${binaryPath}`);
    const binaryExists = fs.existsSync(binaryPath);
    outputChannel.appendLine(`  binary exists: ${binaryExists}`);
    if (!binaryExists) {
        const msg = `BookStack MCP Server: bundled binary not found at '${binaryPath}'. Try reinstalling the extension.`;
        vscode.window.showErrorMessage(msg);
        outputChannel.appendLine(msg);
        return;
    }

    outputChannel.appendLine('  calling registerMcpServerDefinitionProvider...');
    outputChannel.appendLine(`  vscode.lm exists: ${!!vscode.lm}`);
    outputChannel.appendLine(`  registerMcpServerDefinitionProvider exists: ${typeof vscode.lm?.registerMcpServerDefinitionProvider}`);

    let disposable: vscode.Disposable;
    try {
        const onDidChange = new vscode.EventEmitter<void>();
        context.subscriptions.push(onDidChange);

        // Re-trigger provideMcpServerDefinitions (which restarts the server process)
        // whenever any bookstack.* setting changes.
        context.subscriptions.push(
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('bookstack')) {
                    onDidChange.fire();
                }
            })
        );

        disposable = vscode.lm.registerMcpServerDefinitionProvider('bookstack', {
            onDidChangeMcpServerDefinitions: onDidChange.event,
            provideMcpServerDefinitions() {
                const config = vscode.workspace.getConfiguration('bookstack');
                const url = config.get<string>('url', '').trim();
                const tokenId = config.get<string>('tokenId', '').trim();
                const tokenSecret = config.get<string>('tokenSecret', '').trim();
                const scopedBooks = config.get<string[]>('scopedBooks', []);
                const scopedShelves = config.get<string[]>('scopedShelves', []);
                const adminPort = resolveAdminPort();

                const vectorEnabled = config.get<boolean>('vectorSearch.enabled', false);
                const vectorProvider = config.get<string>('vectorSearch.embeddingProvider', 'Ollama');
                const vectorDatabase = config.get<string>('vectorSearch.database', 'Sqlite');
                const ollamaUrl = config.get<string>('vectorSearch.ollamaUrl', '').trim();
                const ollamaModel = config.get<string>('vectorSearch.ollamaModel', '').trim();
                const vectorConn = config.get<string>('vectorSearch.connectionString', '').trim();

                if (!url || !tokenId || !tokenSecret) {
                    vscode.window.showWarningMessage(
                        'BookStack MCP Server: URL, Token ID, and Token Secret must all be configured.',
                        'Open Settings'
                    ).then(selection => {
                        if (selection === 'Open Settings') {
                            vscode.commands.executeCommand('workbench.action.openSettings', 'bookstack');
                        }
                    });
                    outputChannel.appendLine('BookStack MCP Server: one or more settings are blank — server not started.');
                    return [];
                }

                outputChannel.appendLine(`BookStack MCP Server: providing server definition (url=${url}, adminPort=${adminPort}, vectorSearch=${vectorEnabled}).`);

                return [
                    new vscode.McpStdioServerDefinition(
                        'BookStack',
                        binaryPath,
                        [],
                        {
                            BOOKSTACK_BASE_URL: url,
                            BOOKSTACK_TOKEN_SECRET: `${tokenId}:${tokenSecret}`,
                            BOOKSTACK_ADMIN_PORT: String(adminPort),
                            BOOKSTACK_VECTOR_ENABLED: String(vectorEnabled),
                            BOOKSTACK_VECTOR_PROVIDER: vectorProvider,
                            BOOKSTACK_VECTOR_DATABASE: vectorDatabase,
                            ...(ollamaUrl && { BOOKSTACK_VECTOR_OLLAMA_URL: ollamaUrl }),
                            ...(ollamaModel && { BOOKSTACK_VECTOR_OLLAMA_MODEL: ollamaModel }),
                            ...(vectorConn && { BOOKSTACK_VECTOR_CONNECTION: vectorConn }),
                            ...(scopedBooks.length > 0 && { BOOKSTACK_SCOPED_BOOKS: scopedBooks.join(',') }),
                            ...(scopedShelves.length > 0 && { BOOKSTACK_SCOPED_SHELVES: scopedShelves.join(',') }),
                        }
                    )
                ];
            }
        });
    } catch (err) {
        outputChannel.appendLine(`  ERROR calling registerMcpServerDefinitionProvider: ${err}`);
        vscode.window.showErrorMessage(`BookStack MCP Server failed to register: ${err}`);
        return;
    }

    context.subscriptions.push(disposable);
    outputChannel.appendLine('  registerMcpServerDefinitionProvider returned — provider registered.');
    outputChannel.appendLine(`BookStack MCP Server: activation complete (binary=${binaryPath}).`);

    // Admin sidecar integration — status bar, polling, and admin panel.
    const adminPort = resolveAdminPort();
    const adminClient = new AdminSidecarClient(() => adminPort);
    const statusBar = new StatusBarManager(adminClient, 'bookstack.openAdminPanel');
    const adminPanel = new AdminPanelProvider(adminClient, statusBar, context, adminPort);
    statusBar.onStateChange = (state, status) => adminPanel.handleStateChange(state, status);

    context.subscriptions.push(statusBar);
    context.subscriptions.push(adminPanel);
    context.subscriptions.push(
        vscode.commands.registerCommand('bookstack.openAdminPanel', () => {
            adminPanel.openOrFocus();
        })
    );
}

export function deactivate(): void {
    // VS Code disposes subscriptions; no manual cleanup required.
}

function resolveBinaryName(): string | undefined {
    switch (process.platform) {
        case 'win32':  return 'bookstack-mcp-server.exe';
        case 'linux':  return 'bookstack-mcp-server-linux';
        default:       return undefined;
    }
}
