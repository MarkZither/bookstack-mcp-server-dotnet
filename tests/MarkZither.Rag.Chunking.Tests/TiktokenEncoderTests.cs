using FluentAssertions;

namespace MarkZither.Rag.Chunking.Tests;

public sealed class TiktokenEncoderTests
{
    [Test]
    [Arguments("hello world", 2)]
    [Arguments("The quick brown fox jumps over the lazy dog.", 10)]
    [Arguments("こんにちは世界", 4)]
    public void CountTokens_KnownInputs_ReturnsExpectedValue(string input, int expected)
    {
        var encoder = new TiktokenEncoder();

        var count = encoder.CountTokens(input);

        count.Should().Be(expected);
    }
}
