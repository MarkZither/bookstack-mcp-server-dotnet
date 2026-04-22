# BookStack MCP Server

> **AI-powered access to your BookStack knowledge base** — for GitHub Copilot, Claude, and any MCP-compatible AI assistant.

## What It Does

The BookStack MCP Server extension registers a [Model Context Protocol](https://modelcontextprotocol.io/) server that gives your AI assistant direct access to your BookStack instance. Ask Copilot to search pages, list books, read chapters, or create content — all without leaving your editor.

## Prerequisites

- VS Code 1.99 or later with GitHub Copilot (or another MCP-compatible AI assistant)
- A BookStack instance (self-hosted) **or** use the public demo at `https://demo.bookstackapp.com/`
- A BookStack API token (generated in BookStack → **Settings → API Tokens**)
- **No .NET installation required** — the server binary is bundled inside the extension

## Quick Start

1. **Install** this extension from the VS Code Marketplace
2. Open **Settings** (`Ctrl+,`) and search for `bookstack`
3. Set **BookStack: URL** — e.g. `https://demo.bookstackapp.com/` or your self-hosted URL
4. Set **BookStack: Token Id** and **BookStack: Token Secret** from your BookStack API token
5. Open GitHub Copilot Chat and try: `@workspace list all books in BookStack`

> **Demo**: Use `https://demo.bookstackapp.com/` to try without a self-hosted instance. Log in at `https://demo.bookstackapp.com/login`, create an API token under your profile, and use those credentials above.

## Configuration

| Setting | Description |
|---------|-------------|
| `bookstack.url` | Full URL of your BookStack instance, e.g. `https://demo.bookstackapp.com/` |
| `bookstack.tokenId` | API token ID from BookStack → Settings → API Tokens |
| `bookstack.tokenSecret` | API token secret for the token ID above |

## Supported Platforms

| Platform | Status |
|----------|--------|
| Windows (x64) | ✅ Supported |
| Linux (x64) | ✅ Supported |
| macOS | 🔜 Planned |

## Available Tools

Once configured, your AI assistant has access to:

- **Books** — list, create, read, update, delete, export
- **Chapters** — list, create, read, update, delete
- **Pages** — list, create, read, update, delete, export
- **Shelves** — list, create, read, update, delete
- **Search** — full-text search across your BookStack instance

## Troubleshooting

Check the **Output** panel → select **BookStack MCP Server** for activation logs.

| Symptom | Fix |
|---------|-----|
| Warning: settings required | Set `bookstack.url`, `bookstack.tokenId`, and `bookstack.tokenSecret` in Settings |
| 401 Unauthorized | Check token ID and secret are correct and not swapped |
| URL not found | Ensure URL ends with `/` and points to your BookStack root |
| Platform not supported | macOS support is planned — see [#15](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/15) |

## Privacy

This extension sends requests only to the BookStack instance URL you configure. No data is collected by the extension author. Your API token is stored in VS Code settings on your local machine.

## Contributing / Bug Reports

- [GitHub Issues](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues)
- [Source code](https://github.com/MarkZither/bookstack-mcp-server-dotnet)
