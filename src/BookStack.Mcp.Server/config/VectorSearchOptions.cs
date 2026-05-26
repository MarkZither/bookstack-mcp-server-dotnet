using MarkZither.Rag.Chunking;

namespace BookStack.Mcp.Server.Config;

public sealed class VectorSearchOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>"Postgres" | "SqlServer" | "Sqlite"</summary>
    public string Database { get; set; } = VectorSearchDefaults.Database;

    /// <summary>"Ollama" | "AzureOpenAI"</summary>
    public string EmbeddingProvider { get; set; } = VectorSearchDefaults.EmbeddingProvider;

    public OllamaEmbeddingOptions Ollama { get; set; } = new();
    public AzureOpenAIEmbeddingOptions AzureOpenAI { get; set; } = new();
    public VectorSyncOptions Sync { get; set; } = new();
    public ChunkOptions Chunking { get; set; } = new();

    /// <summary>
    /// Dimensionality of the embedding vectors produced by the chosen model.
    /// Must match the value compiled into the provider's entity/schema.
    /// nomic-embed-text: 768 | qllama/bge-large-en-v1.5: 1024 | mxbai-embed-large: 1024 | text-embedding-ada-002: 1536
    /// </summary>
    public int EmbeddingDimensions { get; set; } = VectorSearchDefaults.EmbeddingDimensions;
}

public sealed class OllamaEmbeddingOptions
{
    public string BaseUrl { get; set; } = VectorSearchDefaults.OllamaBaseUrl;
    public string Model { get; set; } = VectorSearchDefaults.OllamaModel;

    /// <summary>
    /// Prefix prepended to query strings before embedding. Improves retrieval quality
    /// for asymmetric models such as mxbai-embed-large. Set to empty string for symmetric
    /// models (nomic-embed-text, qllama/bge-large-en-v1.5).
    /// </summary>
    public string QueryPrefix { get; set; } = VectorSearchDefaults.OllamaQueryPrefix;
}

public sealed class AzureOpenAIEmbeddingOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class VectorSyncOptions
{
    public double IntervalHours { get; set; } = VectorSearchDefaults.SyncIntervalHours;
    public int BatchSize { get; set; } = VectorSearchDefaults.SyncBatchSize;
}

public static class VectorSearchDefaults
{
    public const string Database = "Sqlite";
    public const string EmbeddingProvider = "Ollama";
    public const string OllamaBaseUrl = "http://localhost:11434";
    public const string OllamaModel = "qllama/bge-large-en-v1.5";
    public const string OllamaQueryPrefix = "";
    public const double SyncIntervalHours = 24.0;
    public const int SyncBatchSize = 50;
    public const int EmbeddingDimensions = 1024;
}
