using System.Security.Cryptography;
using System.Text;
using BookStack.Mcp.Server.Api;
using BookStack.Mcp.Server.Api.Models;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Services;
using BookStack.Mcp.Server.Tests.Fakes;
using FluentAssertions;
using MarkZither.Rag.Chunking;
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
    private readonly Mock<IChunkingService> _mockChunkingService = new();

    private static readonly IOptions<VectorSearchOptions> _options = Options.Create(new VectorSearchOptions
    {
        Enabled = true,
        Sync = new VectorSyncOptions { IntervalHours = 10_000 }, // prevent immediate re-loop
        Chunking = new ChunkOptions { ChunkSize = 0 }, // single-chunk fallback for existing tests
    });

    private static readonly IOptions<BookStackApiClientOptions> _clientOptions = Options.Create(new BookStackApiClientOptions
    {
        BaseUrl = "http://bookstack.test",
    });

    private VectorIndexSyncService CreateService() => new(
        _mockStore.Object,
        _mockEmbGen.Object,
        _mockChunkingService.Object,
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

    // T25 — Markdown preference: when Editor == "markdown" and Markdown non-empty, hash and embedding use Markdown text
    [Test]
    public async Task SyncPageAsync_MarkdownEditor_UsesMarkdownTextForHashAndEmbedding()
    {
        const string markdown = "# Hello\n\nMarkdown content.";
        const string html = "<h1>Hello</h1><p>HTML fallback.</p>";

        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page>
            {
                Data = [new Page { Id = 10, BookId = 1, Name = "MD Page", Slug = "md-page" }],
                Total = 1,
            });
        _mockClient.Setup(c => c.GetPageAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 10, Editor = "markdown", Markdown = markdown, Html = html });
        _mockStore.Setup(s => s.GetContentHashAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockStore.Setup(s => s.DeleteChunksAsync(10, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEmbGen
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmbeddings([0.1f]));
        _mockClient.Setup(c => c.GetBookAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 1, Slug = "test-book" });
        _mockStore.Setup(s => s.UpsertAsync(It.IsAny<VectorPageEntry>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        var expectedHash = ComputeHash(markdown);
        _mockStore.Verify(
            s => s.UpsertAsync(
                It.Is<VectorPageEntry>(e => e.ContentHash == expectedHash && e.PageId == 10),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Embedding must be called with the markdown text, not the HTML
        _mockEmbGen.Verify(
            g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(texts => texts.SequenceEqual(new[] { markdown })),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // T26 — HTML fallback: when Editor != "markdown", HTML is used
    [Test]
    public async Task SyncPageAsync_NonMarkdownEditor_UsesHtmlTextForHashAndEmbedding()
    {
        const string html = "<p>HTML content only.</p>";

        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page>
            {
                Data = [new Page { Id = 11, BookId = 1, Name = "HTML Page", Slug = "html-page" }],
                Total = 1,
            });
        _mockClient.Setup(c => c.GetPageAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 11, Editor = "wysiwyg", Html = html });
        _mockStore.Setup(s => s.GetContentHashAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockStore.Setup(s => s.DeleteChunksAsync(11, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEmbGen
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmbeddings([0.2f]));
        _mockClient.Setup(c => c.GetBookAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 1, Slug = "test-book" });
        _mockStore.Setup(s => s.UpsertAsync(It.IsAny<VectorPageEntry>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cycleDone = WireSetLastSyncAt();

        using var service = CreateService();
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        var expectedHash = ComputeHash(html);
        _mockStore.Verify(
            s => s.UpsertAsync(
                It.Is<VectorPageEntry>(e => e.ContentHash == expectedHash && e.PageId == 11),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // T27 — chunked path: IChunkingService called and UpsertAsync called once per chunk
    [Test]
    public async Task SyncPageAsync_ChunkingEnabled_UpsertCalledPerChunk()
    {
        const string html = "<p>Long content to chunk.</p>";
        var chunkingOptions = Options.Create(new VectorSearchOptions
        {
            Enabled = true,
            Sync = new VectorSyncOptions { IntervalHours = 10_000 },
            Chunking = new ChunkOptions { ChunkSize = 512, ChunkOverlap = 128 },
        });

        var chunks = new List<TextChunk>
        {
            new("chunk one", 0, 2, 5),
            new("chunk two", 1, 2, 4),
        };

        _mockChunkingService
            .Setup(c => c.ChunkAsync(html, It.IsAny<ChunkOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _mockStore.Setup(s => s.GetLastSyncAtAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        _mockClient
            .Setup(c => c.GetPagesUpdatedSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListResponse<Page>
            {
                Data = [new Page { Id = 20, BookId = 1, Name = "Chunked", Slug = "chunked" }],
                Total = 1,
            });
        _mockClient.Setup(c => c.GetPageAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageWithContent { Id = 20, Editor = "wysiwyg", Html = html });
        _mockStore.Setup(s => s.GetContentHashAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockStore.Setup(s => s.DeleteChunksAsync(20, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEmbGen
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmbeddings([0.3f]));
        _mockClient.Setup(c => c.GetBookAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookWithContents { Id = 1, Slug = "test-book" });
        _mockStore.Setup(s => s.UpsertAsync(It.IsAny<VectorPageEntry>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cycleDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockStore.Setup(s => s.SetLastSyncAtAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((_, _) => cycleDone.TrySetResult())
            .Returns(Task.CompletedTask);

        VectorIndexSyncService chunkingService = new(
            _mockStore.Object,
            _mockEmbGen.Object,
            _mockChunkingService.Object,
            _mockClient.Object,
            chunkingOptions,
            _clientOptions,
            NullLogger<VectorIndexSyncService>.Instance);

        using var service = chunkingService;
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await cycleDone.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // One UpsertAsync call per chunk
        _mockStore.Verify(
            s => s.UpsertAsync(
                It.Is<VectorPageEntry>(e => e.PageId == 20 && e.ChunkIndex == 0 && e.TotalChunks == 2),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockStore.Verify(
            s => s.UpsertAsync(
                It.Is<VectorPageEntry>(e => e.PageId == 20 && e.ChunkIndex == 1 && e.TotalChunks == 2),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // DeleteChunksAsync called once before upserts
        _mockStore.Verify(
            s => s.DeleteChunksAsync(20, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
