# MarkZither.Rag.Chunking

[![NuGet](https://img.shields.io/nuget/v/MarkZither.Rag.Chunking.svg)](https://www.nuget.org/packages/MarkZither.Rag.Chunking)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Token-aware sliding-window chunking primitives for retrieval-augmented generation (RAG) pipelines in .NET.

Splits long documents into overlapping token-bounded chunks ready for embedding, with optional HTML stripping. No HtmlAgilityPack dependency.

## Installation

```bash
dotnet add package MarkZither.Rag.Chunking --version 0.0.1-alpha.1
```

## Quick Start

```csharp
// Register via DI
services.AddChunking();

// Or resolve manually
ITokenEncoder encoder = new TiktokenEncoder();
IChunkingService chunker = new SlideWindowChunkingService(encoder);

var options = new ChunkOptions
{
    ChunkSize = 512,        // tokens per chunk
    ChunkOverlap = 128,     // overlap tokens between chunks
    MaxChunksPerDocument = 200,
    StripHtml = true        // strip HTML before chunking
};

IReadOnlyList<TextChunk> chunks = await chunker.ChunkAsync(text, options, ct);

foreach (var chunk in chunks)
{
    Console.WriteLine($"[{chunk.ChunkIndex + 1}/{chunk.TotalChunks}] {chunk.TokenCount} tokens: {chunk.Text[..Math.Min(80, chunk.Text.Length)]}...");
}
```

## API

### `IChunkingService`

```csharp
Task<IReadOnlyList<TextChunk>> ChunkAsync(string text, ChunkOptions options, CancellationToken cancellationToken = default);
```

### `ChunkOptions`

| Property | Default | Description |
|---|---|---|
| `ChunkSize` | `512` | Maximum tokens per chunk |
| `ChunkOverlap` | `128` | Token overlap between consecutive chunks |
| `MaxChunksPerDocument` | `200` | Hard cap on chunks produced per document |
| `StripHtml` | `true` | Strip HTML tags before chunking |

### `TextChunk`

```csharp
record TextChunk(string Text, int ChunkIndex, int TotalChunks, int TokenCount);
```

### `ITokenEncoder`

Abstraction over the tokenizer. Default implementation is `TiktokenEncoder` using `cl100k_base` encoding (compatible with `nomic-embed-text`, `text-embedding-ada-002`, and similar models).

### DI Registration

```csharp
// Registers TiktokenEncoder as ITokenEncoder and SlideWindowChunkingService as IChunkingService
services.AddChunking();
```

## Supported Frameworks

- `net9.0`
- `net10.0`

## Versioning

This package follows **SemVer 2.0**. While the version is `0.*`, minor bumps may include breaking changes. See [ADR-0021](https://github.com/MarkZither/bookstack-mcp-server-dotnet/blob/main/docs/architecture/decisions/ADR-0021-rag-chunking-nuget-package.md) for the full versioning policy.

## License

MIT
