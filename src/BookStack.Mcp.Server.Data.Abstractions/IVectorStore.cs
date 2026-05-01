namespace BookStack.Mcp.Server.Data.Abstractions;

public interface IVectorStore
{
    Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topN,
        float minScore,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int pageId, CancellationToken cancellationToken = default);

    Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default);

    Task SetLastSyncAtAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default);
}
