using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace BookStack.Mcp.Server.Data.Sqlite;

public sealed class SqliteVectorStore : IVectorStore
{
    private const string CollectionName = "page_vectors";
    private const string LastSyncKey = "last_sync_at";

    private readonly SqliteCollection<string, VectorPageRecord> _collection;
    private readonly IDbContextFactory<SyncMetadataDbContext> _metaFactory;
    private readonly string _connectionString;

    public SqliteVectorStore(
        string connectionString,
        IDbContextFactory<SyncMetadataDbContext> metaFactory)
    {
        _collection = new SqliteCollection<string, VectorPageRecord>(connectionString, CollectionName);
        _metaFactory = metaFactory;
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        var record = new VectorPageRecord
        {
            StorageKey = BuildStorageKey(entry.PageId, entry.ChunkIndex),
            PageId = entry.PageId,
            ChunkIndex = entry.ChunkIndex,
            TotalChunks = entry.TotalChunks,
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
                ChunkIndex = r.Record.ChunkIndex,
                Title = r.Record.Title,
                Url = r.Record.Url,
                Excerpt = r.Record.Excerpt,
                Score = (float)(r.Score ?? 0),
            });
        }

        return results
            .GroupBy(r => r.PageId)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();
    }

    public async Task DeleteChunksAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2007
        await using var connection = new SqliteConnection(_connectionString);
#pragma warning restore CA2007
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2007
        await using var command = connection.CreateCommand();
#pragma warning restore CA2007
        command.CommandText = $"DELETE FROM {CollectionName} WHERE PageId = $pageId";
        command.Parameters.AddWithValue("$pageId", pageId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await DeleteChunksAsync(pageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        var defaultChunk = await _collection
            .GetAsync(BuildStorageKey(pageId, 0), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (defaultChunk is not null)
        {
            return defaultChunk.ContentHash;
        }

#pragma warning disable CA2007
        await using var connection = new SqliteConnection(_connectionString);
#pragma warning restore CA2007
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2007
        await using var command = connection.CreateCommand();
#pragma warning restore CA2007
        command.CommandText = $"SELECT ContentHash FROM {CollectionName} WHERE PageId = $pageId ORDER BY ChunkIndex LIMIT 1";
        command.Parameters.AddWithValue("$pageId", pageId);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value as string;
    }

    public async Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2007
        await using var db = await _metaFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
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
        await using var db = await _metaFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
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
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS \"Value\" FROM {CollectionName}")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static string BuildStorageKey(int pageId, int chunkIndex)
        => $"{pageId}:{chunkIndex}";
}
