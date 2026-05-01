# ADR-0016: Embedding Provider Abstraction

**Status**: Accepted
**Date**: 2026-04-26
**Author**: GitHub Copilot
**Deciders**: Engineering team

---

## Context

The Vector Search feature (FEAT-0005) requires converting page text to floating-point embedding
vectors at both index time (sync service) and query time (tool handler). Two concrete providers
must be supported:

1. **Ollama** — local, privacy-preserving, no API key required; intended as the default for
   self-hosted deployments. Model: `nomic-embed-text` (configurable).
2. **Azure OpenAI** — cloud-hosted, higher-quality embeddings; intended for users with an Azure
   subscription. Requires endpoint, deployment name, and API key from configuration.

An abstraction is needed to allow the sync service and the tool handler to depend on a stable
interface without coupling to either provider's SDK. The active provider is selected at startup
via the `VectorSearch:EmbeddingProvider` configuration key (`"Ollama"` | `"AzureOpenAI"`).

`Microsoft.Extensions.AI` is Microsoft's official .NET abstraction for AI model access. It
defines `IEmbeddingGenerator<TInput, TEmbedding>` as a standard interface, ships built-in Ollama
and Azure AI Inference extensions, and is the stated direction for the .NET AI ecosystem. The
package reached RC status for .NET 10.

## Decision

We will use `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI` as
the embedding abstraction with no custom wrapper interface.

- When `VectorSearch:EmbeddingProvider` is `"Ollama"`: register
  `OllamaEmbeddingGenerator` (from `Microsoft.Extensions.AI.Ollama`) pointing to
  `VectorSearch:Ollama:BaseUrl` / `VectorSearch:Ollama:Model`.
- When `VectorSearch:EmbeddingProvider` is `"AzureOpenAI"`: register an
  `AzureOpenAIClient`-backed embedding generator (from `Azure.AI.OpenAI` +
  `Microsoft.Extensions.AI` bridge) using `VectorSearch:AzureOpenAI:Endpoint`,
  `VectorSearch:AzureOpenAI:DeploymentName`, and `VectorSearch:AzureOpenAI:ApiKey`.

Both registrations are added as `Singleton<IEmbeddingGenerator<string, Embedding<float>>>` in
the `AddVectorSearch` `IServiceCollection` extension method, guarded by the
`VectorSearch:Enabled` flag.

The Azure OpenAI API key is read exclusively from `IOptions<VectorSearchOptions>` and is never
written to any log output, error string, or MCP tool response (enforced by structured logging
patterns — see NF-4 in the spec).

New NuGet references required in `BookStack.Mcp.Server.csproj`:

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.AI` | `IEmbeddingGenerator<TInput, TEmbedding>`, `Embedding<float>` |
| `Microsoft.Extensions.AI.Ollama` | `OllamaEmbeddingGenerator` |
| `Azure.AI.OpenAI` | `AzureOpenAIClient` (Azure OpenAI provider) |

## Rationale

### Use Microsoft.Extensions.AI, not a custom interface

`Microsoft.Extensions.AI` is the official Microsoft abstraction for AI model access in .NET. It
is backed by the .NET team, already has Ollama and Azure OpenAI first-party extensions, and is
integrated with the `Microsoft.Extensions.DependencyInjection` ecosystem. Building a custom
`IEmbeddingProvider` interface would reproduce the same six-method surface without ecosystem
benefit, and any future third provider (e.g., a local OpenAI-compatible server) would require
writing an adapter instead of pulling a NuGet package.

The one risk — API changes in RC — is mitigated by the fact that `IEmbeddingGenerator` itself
has been stable across the last three preview versions, and the method signature
(`GenerateAsync(IEnumerable<TInput>, EmbeddingGenerationOptions?, ct)`) is unlikely to change
before GA.

### Ollama as default

Ollama is self-hosted, requires no API key, and is already the model server used by many
BookStack self-hosters for local LLMs. Setting it as the default minimises the barrier to
enabling vector search without any cloud dependency.

### API key security

`VectorSearch:AzureOpenAI:ApiKey` is bound via `IOptions<VectorSearchOptions>`, which is
populated from `IConfiguration` (environment variable `VECTORSEARCH__AZUREOPENAI__APIKEY` or
`appsettings.json`). The options object is never serialised, logged, or included in tool
responses; the structured logging constraint (NF-4) is a test requirement for the
`SemanticSearchToolHandlerTests`.

## Alternatives Considered

### Option A: Custom IEmbeddingProvider wrapper interface

- **Pros**: Full control; no dependency on a preview library.
- **Cons**: Reinvents what `Microsoft.Extensions.AI` already provides; requires writing and
  maintaining Ollama and Azure OpenAI adapter implementations; contradicts the .NET AI ecosystem
  direction.
- **Why rejected**: Unnecessary complexity when `IEmbeddingGenerator` meets all requirements
  and is mockable with Moq without additional abstraction layers.

### Option B: Semantic Kernel ITextEmbeddingGenerationService

- **Pros**: Mature abstraction; large ecosystem of SK connectors.
- **Cons**: Pulls in the full Semantic Kernel dependency tree; `ITextEmbeddingGenerationService`
  is broader than required; Semantic Kernel is not a dependency of this project; the official
  Microsoft guidance for new .NET projects is to use `Microsoft.Extensions.AI` directly.
- **Why rejected**: Dependency weight and scope mismatch.

### Option C: Azure OpenAI SDK only (no Ollama)

- **Pros**: GA, well-documented, stable.
- **Cons**: Requires an Azure subscription; violates requirement FR-10 (Ollama as local default);
  no local provider path.
- **Why rejected**: Does not satisfy FEAT-0005 requirements.

## Consequences

### Positive

- `IEmbeddingGenerator<string, Embedding<float>>` is trivially mockable in TUnit tests with Moq
  — no running embedding service required.
- Switching providers (or adding a third, e.g., a locally hosted OpenAI-compatible server)
  requires only a new DI branch, no interface changes.
- `Microsoft.Extensions.AI` integrates directly with `IServiceCollection`, enabling the
  standard `AddEmbeddingGenerator` / `UseLogging` / `UseOpenTelemetry` decorator chain for
  future observability improvements.

### Negative / Trade-offs

- `Microsoft.Extensions.AI` is RC for .NET 10 at the time of this decision; if a breaking API
  change is introduced before GA, the DI wiring code will need updating (limited surface area —
  one `GenerateAsync` call in `VectorIndexSyncService` and one in `SemanticSearchToolHandler`).
- The Ollama and Azure OpenAI NuGet packages are separate, increasing the total dependency count
  by three packages in the main server project.

## Related ADRs

- [ADR-0001: MCP SDK Selection](ADR-0001-mcp-sdk-selection.md)
- [ADR-0015: Vector Store Abstraction and Provider Selection](ADR-0015-vector-store-abstraction.md)
