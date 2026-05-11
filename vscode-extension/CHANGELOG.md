# Changelog

All notable changes to the BookStack MCP Server extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.4.1] - 2026-05-11

### Added

- **Admin Panel — SQLite DB path**: when vector search is enabled with the SQLite provider the
  Admin Panel now shows the resolved database file path with a link that reveals it in the OS file
  manager. Path is `~/.local/share/bookstack-mcp/bookstack-vectors.db` on Linux/macOS and
  `%LOCALAPPDATA%\bookstack-mcp\bookstack-vectors.db` on Windows.
- **README — browsing the SQLite database**: documented the sqlite-vec virtual table constraint
  and how to load the native extension into DB Browser for SQLite so the database can be
  inspected with a GUI tool.

## [0.3.2] - 2026-05-04

### Fixed

- CI: run `dotnet publish` on the runner instead of inside the Docker build stage to avoid OOM (exit code 155).
- CI: bump VS Code extension version to avoid re-publishing an already-published version.

## [0.3.1] - 2026-05-04

### Changed

- Publish Docker image to Docker Hub (`markzither/bookstack-mcp-server`) in addition to GitHub Container Registry.

## [0.3.0] - 2026-05-04

### Added

- **Semantic search** — natural language search across BookStack pages using vector embeddings
  (server-side feature; requires vector search enabled and configured via environment variables or `appsettings.json`).
- Document full MCP tool set in README: Users, Roles, Attachments, Images, Audit Logs, Recycle Bin, Permissions,
  Server Info — these tools have been available since v0.1.0 but were not listed in previous releases.

### Changed

- README: add `bookstack.scopedBooks` and `bookstack.scopedShelves` configuration reference (available since v0.2.0).
- README: add Docker and Streamable HTTP transport sections.

## [0.2.0] - 2026-04-30

### Added

- `bookstack.scopedBooks` setting — restrict the MCP server to specific books by ID or slug.
- `bookstack.scopedShelves` setting — restrict the MCP server to specific shelves by ID or slug.
- Streamable HTTP transport support — the bundled server binary now supports `http` and `both` transports
  (configured via `BOOKSTACK_MCP_TRANSPORT` environment variable; not used by the extension itself which
  always uses stdio).

## [0.1.1] - 2026-04-23

### Changed
- Add `Chat` category and `language-model-tools` keyword so the extension appears in the VS Code Extensions view MCP filter

## [0.1.0] - 2026-04-22

### Added

- Initial release. Bundles BookStack MCP server for win-x64 and linux-x64 (no .NET required).
- `bookstack.url`, `bookstack.tokenId`, and `bookstack.tokenSecret` settings.
- Automatic MCP server registration on activation via `vscode.lm.registerMcpServer`.
- Platform validation — unsupported platforms receive a clear error notification.
- Settings validation — missing settings surface a warning with an "Open Settings" action.
- **BookStack MCP Server** Output channel for activation and diagnostic messages.
- MCP tools: Books, Chapters, Pages, Shelves, Search (list/create/read/update/delete/export).
