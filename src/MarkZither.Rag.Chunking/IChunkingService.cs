namespace MarkZither.Rag.Chunking;

public interface IChunkingService
{
    Task<IReadOnlyList<TextChunk>> ChunkAsync(string text, ChunkOptions options, CancellationToken cancellationToken = default);
}
