using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using MarkZither.Rag.Chunking;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Services;

internal sealed partial class VectorIndexSyncService(
    IVectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IChunkingService chunkingService,
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
                await RunFullSyncAsync(stoppingToken).ConfigureAwait(false);
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

    internal async Task RunFullSyncAsync(CancellationToken ct)
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

    internal async Task SyncPageByUrlAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("SyncPageByUrlAsync: invalid URL '{Url}'.", url);
            return;
        }

        var slug = uri.Segments.LastOrDefault(s => s != "/")?.TrimEnd('/');
        if (string.IsNullOrEmpty(slug))
        {
            logger.LogWarning("SyncPageByUrlAsync: could not extract slug from URL '{Url}'.", url);
            return;
        }

        var searchResult = await apiClient
            .SearchAsync(
                new Api.Models.SearchRequest { Query = $"{slug} type:page", Count = 10 },
                ct)
            .ConfigureAwait(false);

        var match = searchResult.Data.FirstOrDefault(
            r => r.Type == "page" &&
                 string.Equals(r.Url.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            logger.LogWarning(
                "SyncPageByUrlAsync: no page found matching URL '{Url}'.", url);
            return;
        }

        var page = new Api.Models.Page
        {
            Id = match.Id,
            Name = match.Name,
            Slug = match.Slug,
            BookId = match.Book?.Id ?? 0,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await SyncPageAsync(page, new Dictionary<int, string>(), ct).ConfigureAwait(false);
        logger.LogInformation("SyncPageByUrlAsync: indexed page {PageId} ({Url}).", match.Id, url);
    }

    private async Task<bool> SyncPageAsync(
        Page page,
        Dictionary<int, string> bookSlugCache,
        CancellationToken ct)
    {
        var fullPage = await apiClient.GetPageAsync(page.Id, ct).ConfigureAwait(false);

        // Prefer Markdown source when the page was authored in the Markdown editor.
        var useMarkdown = string.Equals(fullPage.Editor, "markdown", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(fullPage.Markdown);
        var inputText = useMarkdown ? fullPage.Markdown! : fullPage.Html;
        var inputFormat = useMarkdown ? "markdown" : "html";

        logger.LogDebug("Page {PageId}: using {Format} as embedding input.", page.Id, inputFormat);

        if (!useMarkdown && DrawIORegex().IsMatch(fullPage.Html))
        {
            logger.LogWarning(
                "Page {PageId} ({Title}) contains DrawIO diagram markup; embeddings may be low quality.",
                page.Id,
                page.Name);
        }

        var contentHash = ComputeSha256(inputText);

        var storedHash = await vectorStore.GetContentHashAsync(page.Id, ct).ConfigureAwait(false);
        if (contentHash == storedHash)
        {
            logger.LogDebug("Page {PageId} content unchanged; skipping.", page.Id);
            return false;
        }

        if (!bookSlugCache.TryGetValue(page.BookId, out var bookSlug))
        {
            var book = await apiClient.GetBookAsync(page.BookId, ct).ConfigureAwait(false);
            bookSlug = book.Slug;
            bookSlugCache[page.BookId] = bookSlug;
        }

        var baseUrl = clientOptions.Value.BaseUrl.TrimEnd('/');
        var chunkOpts = options.Value.Chunking;

        await vectorStore.DeleteChunksAsync(page.Id, ct).ConfigureAwait(false);

        if (chunkOpts.ChunkSize == 0)
        {
            // Single-embedding fallback — no chunking.
            var embeddings = await embeddingGenerator
                .GenerateAsync([inputText], cancellationToken: ct)
                .ConfigureAwait(false);

            var entry = new VectorPageEntry
            {
                PageId = page.Id,
                ChunkIndex = 0,
                TotalChunks = 1,
                Slug = page.Slug,
                Title = page.Name,
                Url = $"{baseUrl}/books/{bookSlug}/pages/{page.Slug}",
                Excerpt = ExtractExcerpt(inputText),
                UpdatedAt = page.UpdatedAt,
                ContentHash = contentHash,
            };

            await vectorStore.UpsertAsync(entry, embeddings[0].Vector, ct).ConfigureAwait(false);
        }
        else
        {
            var chunks = await chunkingService
                .ChunkAsync(inputText, chunkOpts, ct)
                .ConfigureAwait(false);

            for (var i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var chunk = chunks[i];
                var chunkEmbeddings = await embeddingGenerator
                    .GenerateAsync([chunk.Text], cancellationToken: ct)
                    .ConfigureAwait(false);

                var entry = new VectorPageEntry
                {
                    PageId = page.Id,
                    ChunkIndex = chunk.ChunkIndex,
                    TotalChunks = chunk.TotalChunks,
                    Slug = page.Slug,
                    Title = page.Name,
                    Url = $"{baseUrl}/books/{bookSlug}/pages/{page.Slug}",
                    Excerpt = ExtractExcerpt(chunk.Text),
                    UpdatedAt = page.UpdatedAt,
                    ContentHash = contentHash,
                };

                await vectorStore.UpsertAsync(entry, chunkEmbeddings[0].Vector, ct).ConfigureAwait(false);
            }
        }

        logger.LogDebug(
            "Upserted vector(s) for page {PageId} ({Title}), format={Format}.",
            page.Id,
            page.Name,
            inputFormat);

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

    [GeneratedRegex(@"<mxGraphModel|data-drawio|class=""drawio""", RegexOptions.IgnoreCase)]
    private static partial Regex DrawIORegex();
}
