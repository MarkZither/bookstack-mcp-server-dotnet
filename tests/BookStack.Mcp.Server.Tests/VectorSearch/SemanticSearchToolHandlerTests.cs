using System.Text.Json;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Tools.SemanticSearch;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BookStack.Mcp.Server.Tests.VectorSearch;

public sealed class SemanticSearchToolHandlerTests
{
    private readonly Mock<IVectorStore> _mockStore = new();
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockEmbGen = new();

    private static readonly VectorSearchOptions _enabledOptions = new() { Enabled = true };

    private SemanticSearchToolHandler CreateHandler(VectorSearchOptions? options = null)
        => new(
            _mockStore.Object,
            _mockEmbGen.Object,
            Options.Create(options ?? _enabledOptions),
            NullLogger<SemanticSearchToolHandler>.Instance);

    private static GeneratedEmbeddings<Embedding<float>> MakeEmbeddings(float[] vector)
        => new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(vector)]);

    private void SetupEmbedding(float[] vector)
    {
        _mockEmbGen
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeEmbeddings(vector));
    }

    // T25 — empty query returns validation error, no embedding call
    [Test]
    public async Task SemanticSearchAsync_EmptyQuery_ReturnsValidationError()
    {
        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("   ").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
        _mockEmbGen.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // T26a — topN = 0 returns validation error
    [Test]
    public async Task SemanticSearchAsync_TopNZero_ReturnsValidationError()
    {
        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("some query", topN: 0).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
    }

    // T26b — topN = 51 returns validation error
    [Test]
    public async Task SemanticSearchAsync_TopNFiftyOne_ReturnsValidationError()
    {
        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("some query", topN: 51).ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetString().Should().Be("validation_error");
    }

    // T27 — empty vector index returns "[]"
    [Test]
    public async Task SemanticSearchAsync_EmptyIndex_ReturnsEmptyArray()
    {
        SetupEmbedding([1f, 0f]);
        _mockStore
            .Setup(s => s.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("what is on-call policy?").ConfigureAwait(false);

        result.Should().Be("[]");
    }

    // T28 — VectorSearch:Enabled=false returns feature-disabled message
    [Test]
    public async Task SemanticSearchAsync_FeatureDisabled_ReturnsDisabledMessage()
    {
        var handler = CreateHandler(new VectorSearchOptions { Enabled = false });

        var result = await handler.SemanticSearchAsync("any query").ConfigureAwait(false);

        result.Should().Contain("Vector search is disabled");
        _mockEmbGen.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockStore.Verify(
            s => s.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // T29 — populated index returns JSON array sorted by descending score
    [Test]
    public async Task SemanticSearchAsync_PopulatedIndex_ReturnsSortedJsonArray()
    {
        SetupEmbedding([1f, 0f]);
        _mockStore
            .Setup(s => s.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { PageId = 1, Title = "Page A", Url = "http://bs.test/a", Excerpt = "Excerpt A", Score = 0.75f },
                new() { PageId = 2, Title = "Page B", Url = "http://bs.test/b", Excerpt = "Excerpt B", Score = 0.95f },
                new() { PageId = 3, Title = "Page C", Url = "http://bs.test/c", Excerpt = "Excerpt C", Score = 0.85f },
            });

        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("test query").ConfigureAwait(false);

        var doc = JsonDocument.Parse(result);
        var items = doc.RootElement.EnumerateArray().ToList();
        items.Should().HaveCount(3);

        // Verify descending score order
        var scores = items.Select(i => i.GetProperty("score").GetSingle()).ToList();
        scores.Should().BeInDescendingOrder();
        scores[0].Should().BeApproximately(0.95f, 0.001f);
        scores[1].Should().BeApproximately(0.85f, 0.001f);
        scores[2].Should().BeApproximately(0.75f, 0.001f);
    }

    // T30 — min_score=1.0 with sub-threshold results returns "[]"
    [Test]
    public async Task SemanticSearchAsync_MinScoreOneWithSubThresholdResults_ReturnsEmptyArray()
    {
        SetupEmbedding([1f, 0f]);
        _mockStore
            .Setup(s => s.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), 1.0f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var handler = CreateHandler();

        var result = await handler.SemanticSearchAsync("some query", minScore: 1.0f).ConfigureAwait(false);

        result.Should().Be("[]");
    }
}
