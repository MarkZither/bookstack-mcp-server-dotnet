namespace MarkZither.Rag.Chunking;

public sealed record TextChunk(string Text, int ChunkIndex, int TotalChunks, int TokenCount);
