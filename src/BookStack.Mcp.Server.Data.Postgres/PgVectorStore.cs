using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BookStack.Mcp.Server.Data.Postgres;

public sealed class PgVectorStore : IVectorStore
{
    private const string LastSyncKey = "last_sync_at";

    private readonly IDbContextFactory<VectorDbContext> _factory;

    public PgVectorStore(IDbContextFactory<VectorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var existing = await db.PageVectors.FindAsync([entry.PageId], cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.PageVectors.Add(MapToRecord(entry, vector));
        }
        else
        {
            existing.Slug = entry.Slug;
            existing.Title = entry.Title;
            existing.Url = entry.Url;
            existing.Excerpt = entry.Excerpt;
            existing.UpdatedAt = entry.UpdatedAt;
            existing.ContentHash = entry.ContentHash;
            existing.Embedding = new Vector(vector.ToArray());
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topN,
        float minScore,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var query = new Vector(queryVector.ToArray());

        var rows = await db.PageVectors
            .OrderBy(p => p.Embedding!.CosineDistance(query))
            .Take(topN)
            .Select(p => new
            {
                p.PageId,
                p.Title,
                p.Url,
                p.Excerpt,
                Score = 1f - (float)p.Embedding!.CosineDistance(query),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(r => r.Score >= minScore)
            .Select(r => new VectorSearchResult
            {
                PageId = r.PageId,
                Title = r.Title,
                Url = r.Url,
                Excerpt = r.Excerpt,
                Score = r.Score,
            })
            .ToList();
    }

    public async Task DeleteAsync(int pageId, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        await db.PageVectors.Where(p => p.PageId == pageId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        return await db.PageVectors
            .Where(p => p.PageId == pageId)
            .Select(p => p.ContentHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var row = await db.SyncMetadata.FirstOrDefaultAsync(r => r.Key == LastSyncKey, cancellationToken).ConfigureAwait(false);
        if (row is null || !DateTimeOffset.TryParse(row.Value, out var ts))
        {
            return null;
        }

        return ts;
    }

    public async Task SetLastSyncAtAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var row = await db.SyncMetadata.FirstOrDefaultAsync(r => r.Key == LastSyncKey, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            db.SyncMetadata.Add(new SyncMetadataRecord { Key = LastSyncKey, Value = timestamp.ToString("O") });
        }
        else
        {
            row.Value = timestamp.ToString("O");
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static VectorPageRecord MapToRecord(VectorPageEntry entry, ReadOnlyMemory<float> vector) =>
        new()
        {
            PageId = entry.PageId,
            Slug = entry.Slug,
            Title = entry.Title,
            Url = entry.Url,
            Excerpt = entry.Excerpt,
            UpdatedAt = entry.UpdatedAt,
            ContentHash = entry.ContentHash,
            Embedding = new Vector(vector.ToArray()),
        };
}
