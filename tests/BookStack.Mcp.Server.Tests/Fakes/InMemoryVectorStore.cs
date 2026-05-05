using BookStack.Mcp.Server.Data.Abstractions;

namespace BookStack.Mcp.Server.Tests.Fakes;

/// <summary>
/// In-process IVectorStore implementation for unit tests.
/// No database required; cosine similarity computed in C#.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly List<(VectorPageEntry Entry, float[] Vector)> _records = [];
    private DateTimeOffset? _lastSyncAt;

    public Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default)
    {
        _records.RemoveAll(r => r.Entry.PageId == entry.PageId);
        _records.Add((entry, vector.ToArray()));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topN,
        float minScore,
        CancellationToken cancellationToken = default)
    {
        var query = queryVector.ToArray();

        var results = _records
            .Select(r => new VectorSearchResult
            {
                PageId = r.Entry.PageId,
                Title = r.Entry.Title,
                Url = r.Entry.Url,
                Excerpt = r.Entry.Excerpt,
                Score = CosineSimilarity(query, r.Vector),
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    public Task DeleteAsync(int pageId, CancellationToken cancellationToken = default)
    {
        _records.RemoveAll(r => r.Entry.PageId == pageId);
        return Task.CompletedTask;
    }

    public Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var entry = _records.FirstOrDefault(r => r.Entry.PageId == pageId).Entry;
        return Task.FromResult<string?>(entry?.ContentHash);
    }

    public Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_lastSyncAt);

    public Task SetLastSyncAtAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        _lastSyncAt = timestamp;
        return Task.CompletedTask;
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_records.Count);

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0f;
        }

        float dot = 0f, magA = 0f, magB = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }
}
