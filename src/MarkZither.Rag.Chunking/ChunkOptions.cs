namespace MarkZither.Rag.Chunking;

public sealed class ChunkOptions
{
    public int ChunkSize { get; init; } = 256;

    public int ChunkOverlap { get; init; } = 64;

    public int MaxChunksPerDocument { get; init; } = 200;

    public bool StripHtml { get; init; } = true;
}
