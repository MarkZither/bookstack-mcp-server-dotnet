using System.Text;
using MarkZither.Rag.Chunking.Internal;

namespace MarkZither.Rag.Chunking;

public sealed class SlideWindowChunkingService : IChunkingService
{
    private const int MaxTextBytes = 5 * 1024 * 1024;

    private static readonly char[] _boundaryChars = ['.', '!', '?', ';', '\n'];
    private readonly ITokenEncoder _tokenEncoder;

    public SlideWindowChunkingService(ITokenEncoder tokenEncoder)
    {
        _tokenEncoder = tokenEncoder;
    }

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(
        string text,
        ChunkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        ValidateOptions(options);

        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
        {
            throw new ArgumentException($"Input text exceeds {MaxTextBytes} bytes.", nameof(text));
        }

        var normalized = options.StripHtml ? HtmlStripper.Strip(text) : text;
        if (normalized.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<TextChunk>>([]);
        }

        if (options.ChunkSize == 0)
        {
            var fullTokenCount = _tokenEncoder.CountTokens(normalized);
            return Task.FromResult<IReadOnlyList<TextChunk>>([new TextChunk(normalized, 0, 1, fullTokenCount)]);
        }

        var chunks = new List<(string Text, int TokenCount)>();
        var start = 0;

        while (start < normalized.Length && chunks.Count < options.MaxChunksPerDocument)
        {
            cancellationToken.ThrowIfCancellationRequested();
            start = SkipLeadingWhitespace(normalized, start);

            if (start >= normalized.Length)
            {
                break;
            }

            var end = FindMaxEndByTokens(normalized, start, options.ChunkSize, _tokenEncoder);
            if (end <= start)
            {
                end = Math.Min(start + 1, normalized.Length);
            }

            var snappedEnd = SnapBoundary(normalized, start, end);
            if (snappedEnd > start && snappedEnd != end)
            {
                var snappedTokenCount = _tokenEncoder.CountTokens(normalized[start..snappedEnd]);
                if (snappedTokenCount <= options.ChunkSize)
                {
                    end = snappedEnd;
                }
            }

            var chunkText = normalized[start..end];
            var tokenCount = _tokenEncoder.CountTokens(chunkText);

            if (tokenCount > options.ChunkSize)
            {
                end = FindMaxEndByTokens(normalized, start, options.ChunkSize, _tokenEncoder);
                chunkText = normalized[start..end];
                tokenCount = _tokenEncoder.CountTokens(chunkText);
            }

            if (tokenCount == 0)
            {
                start = end;
                continue;
            }

            chunks.Add((chunkText, tokenCount));

            if (end >= normalized.Length)
            {
                break;
            }

            var nextStart = options.ChunkOverlap > 0
                ? FindStartForOverlap(normalized, start, end, options.ChunkOverlap, _tokenEncoder)
                : end;

            start = nextStart <= start ? end : nextStart;
        }

        var total = chunks.Count;
        var result = chunks
            .Select((chunk, index) => new TextChunk(chunk.Text, index, total, chunk.TokenCount))
            .ToList();

        return Task.FromResult<IReadOnlyList<TextChunk>>(result);
    }

    private static void ValidateOptions(ChunkOptions options)
    {
        if (options.ChunkSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkSize), "ChunkSize must be >= 0.");
        }

        if (options.ChunkOverlap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkOverlap), "ChunkOverlap must be >= 0.");
        }

        if (options.ChunkSize > 0 && options.ChunkOverlap > options.ChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkOverlap), "ChunkOverlap must be <= ChunkSize.");
        }

        if (options.MaxChunksPerDocument < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxChunksPerDocument), "MaxChunksPerDocument must be >= 1.");
        }
    }

    private static int SkipLeadingWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static int FindMaxEndByTokens(string text, int start, int maxTokens, ITokenEncoder encoder)
    {
        var low = start + 1;
        var high = text.Length;
        var best = low;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var tokenCount = encoder.CountTokens(text[start..mid]);

            if (tokenCount <= maxTokens)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    private static int FindStartForOverlap(
        string text,
        int chunkStart,
        int chunkEnd,
        int overlapTokens,
        ITokenEncoder encoder)
    {
        var low = chunkStart + 1;
        var high = chunkEnd - 1;
        var candidate = chunkEnd;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var suffixTokenCount = encoder.CountTokens(text[mid..chunkEnd]);

            if (suffixTokenCount >= overlapTokens)
            {
                candidate = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        while (candidate > chunkStart + 1 && !char.IsWhiteSpace(text[candidate - 1]))
        {
            candidate--;
        }

        return candidate;
    }

    private static int SnapBoundary(string text, int start, int end)
    {
        var chunkLength = end - start;
        if (chunkLength < 20)
        {
            return end;
        }

        var window = Math.Max(1, chunkLength / 10);
        var min = Math.Max(start + 1, end - window);
        var max = Math.Min(text.Length - 1, end + window);

        var best = end;
        var bestDistance = int.MaxValue;

        for (var index = min; index <= max; index++)
        {
            if (Array.IndexOf(_boundaryChars, text[index]) < 0)
            {
                continue;
            }

            var candidateEnd = index + 1;
            var distance = Math.Abs(candidateEnd - end);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidateEnd;
            }
        }

        return best;
    }
}
