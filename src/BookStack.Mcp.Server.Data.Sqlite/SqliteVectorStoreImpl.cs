using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace BookStack.Mcp.Server.Data.Sqlite;

public sealed class SqliteVectorStore : IVectorStore
{
    private const string _collectionName = "page_vectors";
    private const string _lastSyncKey = "last_sync_at";

    private readonly SqliteCollection<int, VectorPageRecord> _collection;
    private readonly IDbContextFactory<SyncMetadataDbContext> _metaFactory;

    public SqliteVectorStore(
        string connectionString,
        IDbContextFactory<SyncMetadataDbContext> metaFactory)
    {
        _collection = new SqliteCollection<int, VectorPageRecord>(connectionString, _collectionName);
        _metaFactory = metaFactory;
    }

    public async Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        var record = new VectorPageRecord
        {
            PageId = entry.PageId,
            Slug = entry.Slug,
            Title = entry.Title,
            Url = entry.Url,
            Excerpt = entry.Excerpt,
            UpdatedAtTicks = entry.UpdatedAt.UtcTicks,
            ContentHash = entry.ContentHash,
            Embedding = vector,
        };
        await _collection.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topN,
        float minScore,
        CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<VectorSearchResult>();
        await foreach (var r in _collection.SearchAsync(queryVector, topN, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (r.Score < minScore)
            {
                continue;
            }

            results.Add(new VectorSearchResult
            {
                PageId = r.Record.PageId,
                Title = r.Record.Title,
                Url = r.Record.Url,
                Excerpt = r.Record.Excerpt,
                Score = (float)(r.Score ?? 0),
            });
        }

        return results;
    }

    public async Task DeleteAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        await _collection.DeleteAsync(pageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        var record = await _collection.GetAsync(pageId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return record?.ContentHash;
    }

    public async Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _metaFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var row = await db.SyncMetadata.FirstOrDefaultAsync(r => r.Key == _lastSyncKey, cancellationToken).ConfigureAwait(false);
        if (row is null || !DateTimeOffset.TryParse(row.Value, out var ts))
        {
            return null;
        }

        return ts;
    }

    public async Task SetLastSyncAtAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _metaFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        var row = await db.SyncMetadata.FirstOrDefaultAsync(r => r.Key == _lastSyncKey, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            db.SyncMetadata.Add(new SyncMetadataRecord { Key = _lastSyncKey, Value = timestamp.ToString("O") });
        }
        else
        {
            row.Value = timestamp.ToString("O");
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA2007
        await using var db = await _metaFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
        return await db.Database
            .ExecuteSqlRawAsync("SELECT 1", cancellationToken)
            .ConfigureAwait(false) switch
        {
            _ => await db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS \"Value\" FROM {_collectionName}")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
        };
    }
}
