using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.SemanticSearch;

[McpServerToolType]
internal sealed class SemanticSearchToolHandler(
    IVectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<VectorSearchOptions> options,
    ILogger<SemanticSearchToolHandler> logger)
{
    private readonly IVectorStore _vectorStore = vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator;
    private readonly IOptions<VectorSearchOptions> _options = options;
    private readonly ILogger<SemanticSearchToolHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [McpServerTool(Name = "bookstack_semantic_search")]
    [Description("Search BookStack pages by semantic meaning using vector similarity. Returns pages ranked by relevance to the natural-language query. The vector index must be populated by enabling VectorSearch and allowing the background sync to run.")]
    public async Task<string> SemanticSearchAsync(
        [Description("Natural-language query describing the content to find. Required.")] string query,
        [Description("Maximum number of results to return. Range 1–50. Defaults to 5.")] int topN = 5,
        [Description("Minimum similarity score threshold (0.0–1.0). Results below this score are excluded. Defaults to 0.7.")] float minScore = 0.7f,
        CancellationToken ct = default)
    {
        if (!_options.Value.Enabled)
        {
            return "Vector search is disabled. Set VectorSearch:Enabled to true to use this tool.";
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(
                new { error = "validation_error", message = "query is required and cannot be empty." },
                _jsonOptions);
        }

        if (topN < 1 || topN > 50)
        {
            return JsonSerializer.Serialize(
                new { error = "validation_error", message = "top_n must be between 1 and 50." },
                _jsonOptions);
        }

        try
        {
            var embeddings = await _embeddingGenerator
                .GenerateAsync([query], cancellationToken: ct)
                .ConfigureAwait(false);

            var queryVector = embeddings[0].Vector;

            var results = await _vectorStore
                .SearchAsync(queryVector, topN, minScore, ct)
                .ConfigureAwait(false);

            if (results.Count == 0)
            {
                return "[]";
            }

            var output = results
                .OrderByDescending(r => r.Score)
                .Select(r => new SemanticSearchResultDto(r.PageId, r.Title, r.Url, r.Excerpt, r.Score));

            return JsonSerializer.Serialize(output, _jsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Semantic search failed for query.");
            return JsonSerializer.Serialize(
                new { error = "search_error", message = "An error occurred while performing semantic search." },
                _jsonOptions);
        }
    }

    private sealed record SemanticSearchResultDto(
        [property: JsonPropertyName("pageId")] int PageId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("excerpt")] string Excerpt,
        [property: JsonPropertyName("score")] float Score);
}
