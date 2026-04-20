# ADR-0001: MCP SDK Selection

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The server must expose tools and resources over the Model Context Protocol (MCP) using both `stdio` and Streamable HTTP transports. Implementing MCP from scratch requires writing a JSON-RPC 2.0 message loop, capability negotiation, tool/resource descriptors, progress notifications, and cancellation — all to a moving specification. An official .NET SDK for MCP (`ModelContextProtocol`) was released by Microsoft in 2025 and is now the canonical implementation for .NET consumers.

The decision has significant downstream consequences: the SDK shapes how tool handlers are registered, how transports are configured, and how the dependency injection (DI) graph is composed. Changing it later would require rewriting every tool and resource handler.

## Decision

We will use the **`ModelContextProtocol`** NuGet package (and `ModelContextProtocol.AspNetCore` for Streamable HTTP) as the sole MCP protocol implementation. No hand-rolled JSON-RPC or protocol layer will be written.

## Rationale

- The `ModelContextProtocol` SDK is the official .NET implementation maintained under the `modelcontextprotocol` GitHub organization with active Microsoft involvement, making it the lowest-risk long-term choice for protocol compliance.
- It integrates natively with `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Hosting`, matching the project's DI conventions exactly.
- Both `stdio` and Streamable HTTP transports are first-class concerns in the SDK, eliminating the need for custom transport abstraction.
- The SDK's `[McpServerTool]` and `[McpServerResource]` attribute model maps cleanly onto the one-file-per-handler convention in the project's coding guidelines.
- Using a well-maintained SDK allows contributors to focus on BookStack API integration rather than MCP protocol mechanics, which aligns with the project's open-source learning objective.

## Alternatives Considered

### Option A: Hand-rolled JSON-RPC 2.0 + MCP implementation

- **Pros**: Complete control over message handling, no external dependency, ability to implement only the subset of MCP needed.
- **Cons**: Significant engineering effort to implement capability negotiation, progress notifications, cancellation, and both transport modes correctly; ongoing maintenance burden as the MCP specification evolves; protocol compliance bugs are costly to diagnose.
- **Why rejected**: The engineering cost is disproportionate for a project whose value lies in BookStack integration, not protocol implementation. The `ModelContextProtocol` SDK already handles this correctly.

### Option B: Port the TypeScript reference implementation's protocol layer to C\#

- **Pros**: Direct functional parity with the TypeScript reference implementation; known-good behavior.
- **Cons**: TypeScript idioms (callbacks, dynamic typing) translate poorly to C#; the result would be non-idiomatic C# that fights the type system; maintenance would diverge immediately from both the TypeScript original and the official .NET SDK.
- **Why rejected**: Non-idiomatic, higher maintenance cost, and superseded by the official SDK.

## Consequences

### Positive

- Tool and resource handlers are thin, focused classes decorated with SDK attributes — no boilerplate transport or serialization code.
- Protocol upgrades are handled by updating the `ModelContextProtocol` NuGet version, not by changing application code.
- Full compatibility with the MCP specification is guaranteed by the SDK's own test suite.
- `stdio` and Streamable HTTP are both configurable at startup with minimal code, satisfying the dual-transport requirement.

### Negative / Trade-offs

- The project takes a dependency on a relatively young NuGet package; breaking changes between minor versions are possible while the MCP specification is still evolving.
- Debugging protocol-level issues requires understanding the SDK's internals or reading its source code rather than the project's own code.
- SDK limitations (e.g., unsupported MCP features) would require either waiting for SDK updates or contributing upstream.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
