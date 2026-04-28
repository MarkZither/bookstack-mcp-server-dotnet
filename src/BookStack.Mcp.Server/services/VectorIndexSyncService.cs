using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Services;

internal sealed partial class VectorIndexSyncService(
    IVectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IBookStackApiClient apiClient,
    IOptions<VectorSearchOptions> options,
    IOptions<BookStackApiClientOptions> clientOptions,
    ILogger<VectorIndexSyncService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "VectorIndexSyncService started. Sync interval: {Hours}h",
            options.Value.Sync.IntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vector index sync cycle failed unexpectedly.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromHours(options.Value.Sync.IntervalHours),
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("VectorIndexSyncService stopped.");
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        var lastSyncAt = await vectorStore.GetLastSyncAtAsync(ct).ConfigureAwait(false)
            ?? DateTimeOffset.MinValue;

        logger.LogInformation("Starting vector index sync. Last sync: {LastSync}", lastSyncAt);

        var response = await apiClient
            .GetPagesUpdatedSinceAsync(lastSyncAt, cancellationToken: ct)
            .ConfigureAwait(false);

        logger.LogInformation("Found {Count} pages to process.", response.Data.Count);

        var bookSlugCache = new Dictionary<int, string>();
        var synced = 0;
        var skipped = 0;

        foreach (var page in response.Data)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var wasUpserted = await SyncPageAsync(page, bookSlugCache, ct).ConfigureAwait(false);
                if (wasUpserted)
                {
                    synced++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to sync page {PageId} ({Title}); skipping.",
                    page.Id,
                    page.Name);
                skipped++;
            }
        }

        await vectorStore.SetLastSyncAtAsync(DateTimeOffset.UtcNow, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Vector index sync complete. Upserted: {Synced}, Skipped: {Skipped}.",
            synced,
            skipped);
    }

    private async Task<bool> SyncPageAsync(
        Page page,
        Dictionary<int, string> bookSlugCache,
        CancellationToken ct)
    {
        var fullPage = await apiClient.GetPageAsync(page.Id, ct).ConfigureAwait(false);
        var contentHash = ComputeSha256(fullPage.Html);

        var storedHash = await vectorStore.GetContentHashAsync(page.Id, ct).ConfigureAwait(false);
        if (contentHash == storedHash)
        {
            logger.LogDebug("Page {PageId} content unchanged; skipping.", page.Id);
            return false;
        }

        var embeddings = await embeddingGenerator
            .GenerateAsync([fullPage.Html], cancellationToken: ct)
            .ConfigureAwait(false);
        var vector = embeddings[0].Vector;

        if (!bookSlugCache.TryGetValue(page.BookId, out var bookSlug))
        {
            var book = await apiClient.GetBookAsync(page.BookId, ct).ConfigureAwait(false);
            bookSlug = book.Slug;
            bookSlugCache[page.BookId] = bookSlug;
        }

        var baseUrl = clientOptions.Value.BaseUrl.TrimEnd('/');
        var entry = new VectorPageEntry
        {
            PageId = page.Id,
            Slug = page.Slug,
            Title = page.Name,
            Url = $"{baseUrl}/books/{bookSlug}/pages/{page.Slug}",
            Excerpt = ExtractExcerpt(fullPage.Html),
            UpdatedAt = page.UpdatedAt,
            ContentHash = contentHash,
        };

        await vectorStore.UpsertAsync(entry, vector, ct).ConfigureAwait(false);

        logger.LogDebug("Upserted vector for page {PageId} ({Title}).", page.Id, page.Name);

        return true;
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string ExtractExcerpt(string htmlContent)
    {
        var plain = StripTagsRegex().Replace(htmlContent, " ");
        plain = WhitespaceRegex().Replace(plain, " ").Trim();
        return plain.Length <= 300 ? plain : plain[..300];
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex StripTagsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
