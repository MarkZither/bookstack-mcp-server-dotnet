# Envisioning: bookstack-mcp-server-dotnet

**Author**: OSS Community
**Created**: 2026-04-20
**Last Updated**: 2026-04-20
**Status**: Approved

---

## Problem Statement

Knowledge lives in enterprise wikis (BookStack) but is not discoverable at the moment developers need it. Developers stop to search, context-switch, and often fail to find the right page — or don't know it exists. Existing MCP integrations for BookStack are TypeScript-only, leaving .NET teams without a native option. BookStack's built-in search is keyword-based and misses semantically related content, so even when developers try to find answers, poor search quality returns incomplete or irrelevant results.

## Vision

Twelve months from now, any developer on a .NET or AI-agent stack can point an MCP client (VS Code, Visual Studio, or any LLM tool) at this server and have BookStack knowledge surfaced automatically — in context, on demand. Semantic search powered by a vector index makes every wiki page discoverable by meaning, not just keywords. The server achieves full feature parity with the TypeScript reference implementation and is distributed through the VS Code and Visual Studio extension marketplaces, making adoption frictionless for enterprise .NET teams.

## Goals

1. Achieve full feature parity with the [TypeScript reference implementation](https://github.com/pnocera/bookstack-mcp-server) (47+ MCP tools and resources).
2. Implement vector indexing and semantic search over BookStack pages to overcome the platform's keyword-only search limitation.
3. Publish the MCP server to the VS Code and Visual Studio extension marketplaces so .NET teams can install it with a single click.
4. Support both `stdio` and Streamable HTTP transports so the server is usable from CLI agents, IDE extensions, and hosted workflows.
5. Target BookStack API v25 as the initial baseline and keep pace with future API versions.

## Non-Goals

- Replacing or forking BookStack itself — this project is a read/write client only.
- Supporting non-.NET runtimes or providing a TypeScript/Python SDK.
- Building a custom BookStack UI or browser extension.
- Enterprise SLA, paid support, or managed hosting — this is an open-source learning project.

## Key Scenarios

### Scenario 1: Developer asks a question and gets a wiki answer

> **As** an AI agent developer, **I want** my LLM to query BookStack via MCP **so that** it can retrieve relevant documentation pages and include them in its response without manual search.

A developer is working in VS Code with a Copilot agent. They ask a question about an internal API. The MCP server receives a semantic search query, runs it against the vector index, and returns the top matching BookStack pages. The agent cites the source and answers without the developer ever opening a browser.

### Scenario 2: .NET enterprise team adopts MCP

> **As** a .NET enterprise team, **I want** a native .NET MCP server for BookStack **so that** I can integrate knowledge retrieval into our existing .NET-based AI toolchain without bridging to Node.js.

The team adds the NuGet-distributed server to their .NET AI pipeline. They configure it with a BookStack token and immediately get access to all books, chapters, pages, shelves, and users via strongly typed MCP tools — no TypeScript runtime required.

### Scenario 3: Semantic search surfaces related knowledge

> **As** an LLM developer, **I want** semantic search over BookStack pages **so that** my agent finds conceptually related content even when the exact keywords are not present.

A query for "how do we deploy to production" returns pages titled "Release Process", "CI/CD Pipeline", and "Environment Configuration" — none of which contain the literal phrase — because they are semantically similar. BookStack's built-in keyword search would have returned zero results.

### Scenario 4: Installing via the extension marketplace

> **As** a developer using VS Code or Visual Studio, **I want** to install the BookStack MCP server from the extensions marketplace **so that** I can get started without manual CLI setup.

The developer searches "BookStack MCP" in the VS Code marketplace, installs the extension, enters their BookStack URL and API token in settings, and the MCP server is live within two minutes.

## Proposed Approach (High Level)

- **Runtime**: .NET 10 / C# with `async`/`await` throughout and `System.Text.Json` for serialization.
- **MCP SDK**: Leverage the official .NET MCP SDK to expose tools and resources over `stdio` and Streamable HTTP transports.
- **API client**: Typed `HttpClient` wrappers against the BookStack REST API (v25 baseline).
- **Semantic search**: Embed BookStack page content using a configurable embedding model; store vectors in a local or pluggable vector database; expose a semantic search MCP tool alongside the standard keyword search.
- **Distribution**: Package as a VS Code extension (via `mcp` server manifest) and a Visual Studio extension; also distribute as a standalone .NET tool on NuGet.
- **Validation and security**: Validate all external inputs at system boundaries; never log API tokens; follow OWASP Top 10 throughout.

## Open Questions

- [ ] Which vector database should be the default? (e.g., in-process `Microsoft.SemanticKernel` memory, Qdrant, or SQLite-vec)
- [ ] Which embedding model should be the default, and how is it configured? (local ONNX vs. Azure OpenAI vs. OpenAI API)
- [ ] How frequently should the vector index be refreshed, and should incremental indexing be supported?
- [ ] What is the packaging story for VS Code / Visual Studio marketplaces — separate VSIX per IDE or shared manifest?

## Success Metrics

| Metric | Baseline | Target |
|---|---|---|
| MCP tool/resource coverage | 0 (greenfield) | 47+ tools — full parity with TypeScript reference |
| Semantic search capability | Not available | Vector index over all BookStack pages, returning top-N semantically similar results |
| Marketplace availability | Not published | Listed on VS Code Marketplace and Visual Studio Marketplace |
| Transport support | None | `stdio` and Streamable HTTP both functional |
| BookStack API coverage | None | BookStack API v25 fully covered |
