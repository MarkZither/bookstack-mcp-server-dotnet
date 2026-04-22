# Changelog

All notable changes to the BookStack MCP Server extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.1.0] - 2026-04-22

### Added

- Initial release. Bundles BookStack MCP server for win-x64 and linux-x64 (no .NET required).
- `bookstack.url`, `bookstack.tokenId`, and `bookstack.tokenSecret` settings.
- Automatic MCP server registration on activation via `vscode.lm.registerMcpServer`.
- Platform validation — unsupported platforms receive a clear error notification.
- Settings validation — missing settings surface a warning with an "Open Settings" action.
- **BookStack MCP Server** Output channel for activation and diagnostic messages.
- MCP tools: Books, Chapters, Pages, Shelves, Search (list/create/read/update/delete/export).
