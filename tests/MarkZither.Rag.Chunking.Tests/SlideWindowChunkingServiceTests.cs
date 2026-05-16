using System.Text;
using FluentAssertions;

namespace MarkZither.Rag.Chunking.Tests;

public sealed class SlideWindowChunkingServiceTests
{
    [Test]
    public async Task ChunkAsync_ChunkSizeZero_ReturnsSingleFullChunk()
    {
        var service = new SlideWindowChunkingService(new WordTokenEncoder());
        var input = "one two three";

        var chunks = await service.ChunkAsync(input, new ChunkOptions { ChunkSize = 0 }).ConfigureAwait(false);

        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(input);
        chunks[0].TokenCount.Should().Be(3);
    }

    [Test]
    public async Task ChunkAsync_MultiChunk_RespectsChunkSizeAndOverlap()
    {
        var encoder = new WordTokenEncoder();
        var service = new SlideWindowChunkingService(encoder);
        var input = string.Join(' ', Enumerable.Range(1, 30).Select(i => $"word{i}"));

        var chunks = await service.ChunkAsync(input, new ChunkOptions
        {
            ChunkSize = 10,
            ChunkOverlap = 3,
            MaxChunksPerDocument = 20,
            StripHtml = false,
        }).ConfigureAwait(false);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.TokenCount <= 10);

        for (var index = 0; index < chunks.Count - 1; index++)
        {
            var overlap = SharedLeadingTrailingWords(chunks[index].Text, chunks[index + 1].Text);
            overlap.Should().BeGreaterThanOrEqualTo(3);
        }
    }

    [Test]
    public async Task ChunkAsync_RespectsMaxChunksPerDocument()
    {
        var service = new SlideWindowChunkingService(new WordTokenEncoder());
        var input = string.Join(' ', Enumerable.Range(1, 100).Select(i => $"word{i}"));

        var chunks = await service.ChunkAsync(input, new ChunkOptions
        {
            ChunkSize = 5,
            ChunkOverlap = 2,
            MaxChunksPerDocument = 3,
            StripHtml = false,
        }).ConfigureAwait(false);

        chunks.Should().HaveCount(3);
    }

    [Test]
    public async Task ChunkAsync_SnapsToSentenceBoundaryWhenPossible()
    {
        var service = new SlideWindowChunkingService(new CharacterTokenEncoder());
        var input = "Sentence one. Sentence two has extra words. Sentence three.";

        var chunks = await service.ChunkAsync(input, new ChunkOptions
        {
            ChunkSize = 20,
            ChunkOverlap = 5,
            MaxChunksPerDocument = 10,
            StripHtml = false,
        }).ConfigureAwait(false);

        chunks[0].Text.Should().Contain(".");
    }

    [Test]
    public async Task ChunkAsync_TextOverFiveMegabytes_Throws()
    {
        var service = new SlideWindowChunkingService(new CharacterTokenEncoder());
        var largeText = new string('a', (5 * 1024 * 1024) + 1);

        var act = async () => await service.ChunkAsync(
            largeText,
            new ChunkOptions { ChunkSize = 512, ChunkOverlap = 0, StripHtml = false }).ConfigureAwait(false);

        await act.Should().ThrowAsync<ArgumentException>().ConfigureAwait(false);
    }

    private static int SharedLeadingTrailingWords(string leftChunk, string rightChunk)
    {
        var left = leftChunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var right = rightChunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var max = Math.Min(left.Length, right.Length);

        for (var size = max; size > 0; size--)
        {
            var leftSuffix = left.Skip(left.Length - size);
            var rightPrefix = right.Take(size);
            if (leftSuffix.SequenceEqual(rightPrefix))
            {
                return size;
            }
        }

        return 0;
    }

    private sealed class WordTokenEncoder : ITokenEncoder
    {
        public int CountTokens(string text)
            => string.IsNullOrWhiteSpace(text)
                ? 0
                : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private sealed class CharacterTokenEncoder : ITokenEncoder
    {
        public int CountTokens(string text) => Encoding.UTF8.GetByteCount(text);
    }
}
