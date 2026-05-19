using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Tests.Fakes;
using BookStack.Mcp.Server.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Tests.VectorSearch;

public sealed class VectorSearchIntegrationTests
{
    // T31 — UpsertAsync then SearchAsync returns expected result
    [Test]
    public async Task InMemoryVectorStore_UpsertThenSearch_ReturnsMatchingEntry()
    {
        var store = new InMemoryVectorStore();
        var entry = new VectorPageEntry
        {
            PageId = 42,
            Title = "Reset Password",
            Url = "http://bs.test/books/help/pages/reset-password",
            Excerpt = "How to reset your password.",
            Slug = "reset-password",
            UpdatedAt = DateTimeOffset.UtcNow,
            ContentHash = "abc123",
        };

        // Unit vector along first dimension — cosine similarity with identical query = 1.0
        var vector = new float[] { 1f, 0f, 0f };
        await store.UpsertAsync(entry, vector).ConfigureAwait(false);

        var results = await store.SearchAsync(
            queryVector: new float[] { 1f, 0f, 0f },
            topN: 5,
            minScore: 0.9f).ConfigureAwait(false);

        results.Should().HaveCount(1);
        results[0].PageId.Should().Be(42);
        results[0].Title.Should().Be("Reset Password");
        results[0].Score.Should().BeApproximately(1.0f, 0.001f);
    }

    // T32 — GetContentHashAsync returns null before upsert, stored hash after
    [Test]
    public async Task InMemoryVectorStore_GetContentHashAsync_ReturnsNullBeforeUpsert_HashAfter()
    {
        var store = new InMemoryVectorStore();

        var hashBefore = await store.GetContentHashAsync(99).ConfigureAwait(false);
        hashBefore.Should().BeNull();

        var entry = new VectorPageEntry
        {
            PageId = 99,
            ContentHash = "deadbeef",
            Title = "Page",
            Slug = "page",
            Url = "http://bs.test/page",
            Excerpt = string.Empty,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await store.UpsertAsync(entry, new float[] { 0.5f }).ConfigureAwait(false);

        var hashAfter = await store.GetContentHashAsync(99).ConfigureAwait(false);
        hashAfter.Should().Be("deadbeef");
    }

    [Test]
    public async Task InMemoryVectorStore_SearchAsync_DeduplicatesByPageId_UsesHighestScoreChunk()
    {
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 10,
                ChunkIndex = 0,
                TotalChunks = 2,
                Title = "Runbook",
                Url = "http://bs.test/books/ops/pages/runbook",
                Excerpt = "lower scoring chunk",
                Slug = "runbook",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-10",
            },
            new float[] { 0.2f, 0.98f, 0f }).ConfigureAwait(false);

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 10,
                ChunkIndex = 1,
                TotalChunks = 2,
                Title = "Runbook",
                Url = "http://bs.test/books/ops/pages/runbook",
                Excerpt = "higher scoring chunk",
                Slug = "runbook",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-10",
            },
            new float[] { 1f, 0f, 0f }).ConfigureAwait(false);

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 11,
                ChunkIndex = 0,
                TotalChunks = 1,
                Title = "Other",
                Url = "http://bs.test/books/ops/pages/other",
                Excerpt = "other page",
                Slug = "other",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-11",
            },
            new float[] { 0.9f, 0f, 0f }).ConfigureAwait(false);

        var results = await store.SearchAsync(new float[] { 1f, 0f, 0f }, topN: 10, minScore: 0.0f).ConfigureAwait(false);

        results.Count(r => r.PageId == 10).Should().Be(1);
        results.Should().Contain(r => r.PageId == 10 && r.ChunkIndex == 1 && r.Excerpt == "higher scoring chunk");
    }

    [Test]
    public async Task InMemoryVectorStore_DeleteChunksAsync_RemovesAllChunksForPage()
    {
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 20,
                ChunkIndex = 0,
                TotalChunks = 2,
                Title = "Page 20",
                Url = "http://bs.test/page-20",
                Excerpt = "chunk 0",
                Slug = "page-20",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-20",
            },
            new float[] { 1f, 0f, 0f }).ConfigureAwait(false);

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 20,
                ChunkIndex = 1,
                TotalChunks = 2,
                Title = "Page 20",
                Url = "http://bs.test/page-20",
                Excerpt = "chunk 1",
                Slug = "page-20",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-20",
            },
            new float[] { 0.9f, 0f, 0f }).ConfigureAwait(false);

        await store.UpsertAsync(
            new VectorPageEntry
            {
                PageId = 21,
                ChunkIndex = 0,
                TotalChunks = 1,
                Title = "Page 21",
                Url = "http://bs.test/page-21",
                Excerpt = "other page",
                Slug = "page-21",
                UpdatedAt = DateTimeOffset.UtcNow,
                ContentHash = "hash-21",
            },
            new float[] { 0.8f, 0f, 0f }).ConfigureAwait(false);

        await store.DeleteChunksAsync(20).ConfigureAwait(false);

        var results = await store.SearchAsync(new float[] { 1f, 0f, 0f }, topN: 10, minScore: 0.0f).ConfigureAwait(false);
        results.Should().NotContain(r => r.PageId == 20);
        results.Should().Contain(r => r.PageId == 21);
    }

    // T33 — GetPagesUpdatedSinceAsync sends correct filter query parameter
    [Test]
    public async Task BookStackApiClient_GetPagesUpdatedSinceAsync_SendsCorrectFilterParameter()
    {
        var json = """{"data":[],"total":0}""";
        var handler = MockHttpMessageHandler.ReturningJson(json);

        var options = Options.Create(new BookStackApiClientOptions
        {
            BaseUrl = "http://bookstack.test",
            TokenId = "tid",
            TokenSecret = "tsecret",
            TimeoutSeconds = 30,
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://bookstack.test/api/"),
        };
        var client = new BookStack.Mcp.Server.Api.BookStackApiClient(
            httpClient, options, NullLogger<BookStack.Mcp.Server.Api.BookStackApiClient>.Instance);

        var since = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        await client.GetPagesUpdatedSinceAsync(since).ConfigureAwait(false);

        var requestUri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        requestUri.Should().Contain("pages");
        requestUri.Should().Contain("filter");
        requestUri.Should().Contain("2025-06-01");
    }
}
