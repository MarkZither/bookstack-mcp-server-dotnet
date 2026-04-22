import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

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
        disposable = vscode.lm.registerMcpServerDefinitionProvider('bookstack', {
            provideMcpServerDefinitions() {
                const config = vscode.workspace.getConfiguration('bookstack');
                const url = config.get<string>('url', '').trim();
                const tokenId = config.get<string>('tokenId', '').trim();
                const tokenSecret = config.get<string>('tokenSecret', '').trim();

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

                outputChannel.appendLine(`BookStack MCP Server: providing server definition (url=${url}).`);

                return [
                    new vscode.McpStdioServerDefinition(
                        'BookStack',
                        binaryPath,
                        [],
                        {
                            BOOKSTACK_BASE_URL: url,
                            BOOKSTACK_TOKEN_SECRET: `${tokenId}:${tokenSecret}`,
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
