using System.Security.Cryptography;
using System.Text;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Services;
using BookStack.Mcp.Server.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BookStack.Mcp.Server.Tests.VectorSearch;

public sealed class VectorIndexSyncServiceTests
{
    private readonly Mock<IVectorStore> _mockStore = new();
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockEmbGen = new();
    private readonly Mock<IBookStackApiClient> _mockClient = new();

    private static readonly IOptions<VectorSearchOptions> _options = Options.Create(new VectorSearchOptions
    {
        Enabled = true,
        Sync = new VectorSyncOptions { IntervalHours = 10_000 }, // prevent immediate re-loop
    });

    private static readonly IOptions<BookStackApiClientOptions> _clientOptions = Options.Create(new BookStackApiClientOptions
    {
        BaseUrl = "http://bookstack.test",
    });

    private VectorIndexSyncService CreateService() => new(
        _mockStore.Object,
        _mockEmbGen.Object,
        _mockClient.Object,
        _options,
        _clientOptions,
        NullLogger<VectorIndexSyncService>.Instance);

    private static string ComputeHash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static GeneratedEmbeddings<Embedding<float>> MakeEmbeddings(float[] vector)
        => new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(vector)]);

    private TaskCompletionSource WireSetLastSyncAt()
    {
        var cycleDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockStore.Setup(s => s.SetLastSyncAtAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((_, _) => cycleDone.TrySetResult())
            .Returns(Task.CompletedTask);
        return cycleDone;
    }

    // T21 — unchanged content hash: skips embedding generator
    [Test]
    public async Task SyncCycle_UnchangedContentHash_DoesNotCallEmbeddingGenerator()
    {
        const string html = "<p>Existing content</p>";
        var hash = ComputeHash(html);

        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page> { Data = [new Page { Id = 1, BookId = 1 }], Total = 1 });
        _mockClient.Setup(c => c.GetPageAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 1, Html = html });
        _mockStore.Setup(s => s.GetContentHashAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash); // already up to date

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        _mockEmbGen.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // T22 — new page: UpsertAsync called with correct metadata
    [Test]
    public async Task SyncCycle_NewPage_CallsUpsertWithCorrectMetadata()
    {
        const string html = "<p>New page content</p>";
        var updatedAt = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page>
            {
                Data = [new Page { Id = 7, BookId = 3, Name = "Test Page", Slug = "test-page", UpdatedAt = updatedAt }],
                Total = 1,
            });
        _mockClient.Setup(c => c.GetPageAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 7, Html = html });
        _mockStore.Setup(s => s.GetContentHashAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockEmbGen
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmbeddings([0.5f, 0.5f]));
        _mockClient.Setup(c => c.GetBookAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 3, Slug = "test-book" });
        _mockStore.Setup(s => s.UpsertAsync(It.IsAny<VectorPageEntry>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        _mockStore.Verify(
            s => s.UpsertAsync(
                It.Is<VectorPageEntry>(e =>
                    e.PageId == 7 &&
                    e.Title == "Test Page" &&
                    e.Slug == "test-page" &&
                    e.Url == "http://bookstack.test/books/test-book/pages/test-page" &&
                    e.UpdatedAt == updatedAt),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // T23 — per-page embedding failure: cycle continues for remaining pages
    [Test]
    public async Task SyncCycle_EmbeddingFailsForOnePage_ContinuesAndSyncsOtherPages()
    {
        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page>
            {
                Data =
                [
                    new Page { Id = 1, BookId = 1, Name = "Page One", Slug = "page-one" },
                    new Page { Id = 2, BookId = 1, Name = "Page Two", Slug = "page-two" },
                ],
                Total = 2,
            });
        _mockClient.Setup(c => c.GetPageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new PageWithContent { Id = id, Html = $"<p>Content {id}</p>" });
        _mockStore.Setup(s => s.GetContentHashAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockEmbGen
            .SetupSequence(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service unavailable"))
            .ReturnsAsync(MakeEmbeddings([0.5f, 0.5f]));

        _mockClient.Setup(c => c.GetBookAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 1, Slug = "test-book" });
        _mockStore.Setup(s => s.UpsertAsync(It.IsAny<VectorPageEntry>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // Only page 2 should have been upserted (page 1 embedding failed)
        _mockStore.Verify(
            s => s.UpsertAsync(It.Is<VectorPageEntry>(e => e.PageId == 2), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockStore.Verify(
            s => s.UpsertAsync(It.Is<VectorPageEntry>(e => e.PageId == 1), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // T24 — SetLastSyncAtAsync called exactly once per successful cycle
    [Test]
    public async Task SyncCycle_Success_CallsSetLastSyncAtOnce()
    {
        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page> { Data = [], Total = 0 });

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        _mockStore.Verify(
            s => s.SetLastSyncAtAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
