import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export function activate(context: vscode.ExtensionContext): void {
    const outputChannel = vscode.window.createOutputChannel('BookStack MCP Server');
    context.subscriptions.push(outputChannel);

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
        outputChannel.appendLine('BookStack MCP Server: activation skipped — one or more settings are blank.');
        return;
    }

    const binaryName = resolveBinaryName();
    if (!binaryName) {
        vscode.window.showErrorMessage(
            `BookStack MCP Server: platform '${process.platform}' is not supported in this release. ` +
            'See https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15 for status.'
        );
        outputChannel.appendLine(`BookStack MCP Server: unsupported platform '${process.platform}'.`);
        return;
    }

    const binaryPath = path.join(context.extensionPath, 'bin', binaryName);
    if (!fs.existsSync(binaryPath)) {
        vscode.window.showErrorMessage(
            `BookStack MCP Server: bundled binary not found at '${binaryPath}'. ` +
            'Try reinstalling the extension.'
        );
        outputChannel.appendLine(`BookStack MCP Server: binary not found at '${binaryPath}'.`);
        return;
    }

    const combinedToken = `${tokenId}:${tokenSecret}`;
    const env: Record<string, string> = {
        BOOKSTACK_BASE_URL: url,
        BOOKSTACK_TOKEN_SECRET: combinedToken,
    };

    vscode.lm.registerMcpServer({
        name: 'bookstack',
        command: binaryPath,
        args: [],
        env,
    });

    outputChannel.appendLine(`BookStack MCP Server: registered (${binaryPath}, url=${url}).`);
}

export function deactivate(): void {
    // VS Code manages the MCP server process lifecycle; no cleanup required.
}

function resolveBinaryName(): string | undefined {
    switch (process.platform) {
        case 'win32':  return 'bookstack-mcp-server.exe';
        case 'linux':  return 'bookstack-mcp-server-linux';
        default:       return undefined;
    }
}
