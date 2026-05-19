using Microsoft.Extensions.VectorData;

namespace BookStack.Mcp.Server.Data.Sqlite;

public sealed class VectorPageRecord
{
    [VectorStoreKey]
    public string StorageKey { get; set; } = string.Empty;

    [VectorStoreData]
    public int PageId { get; set; }

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public int TotalChunks { get; set; } = 1;

    [VectorStoreData]
    public string Slug { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Url { get; set; } = string.Empty;

    [VectorStoreData]
    public string Excerpt { get; set; } = string.Empty;

    [VectorStoreData]
    public long UpdatedAtTicks { get; set; }

    [VectorStoreData]
    public string ContentHash { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 768)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
