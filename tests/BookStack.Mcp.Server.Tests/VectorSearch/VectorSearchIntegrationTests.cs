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
