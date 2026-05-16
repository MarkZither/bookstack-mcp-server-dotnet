using Tiktoken;

namespace MarkZither.Rag.Chunking;

public sealed class TiktokenEncoder : ITokenEncoder
{
    private const string DefaultModel = "text-embedding-ada-002";

    private readonly Func<string, int> _countTokens;

    public TiktokenEncoder()
    {
        var encoder = ModelToEncoder.For(DefaultModel);
        _countTokens = encoder.CountTokens;
    }

    public int CountTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _countTokens(text);
    }
}
