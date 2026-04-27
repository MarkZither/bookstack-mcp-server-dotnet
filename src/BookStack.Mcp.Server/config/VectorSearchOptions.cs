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
}

public sealed class OllamaEmbeddingOptions
{
    public string BaseUrl { get; set; } = VectorSearchDefaults.OllamaBaseUrl;
    public string Model { get; set; } = VectorSearchDefaults.OllamaModel;
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
    public const string OllamaModel = "nomic-embed-text";
    public const double SyncIntervalHours = 24.0;
    public const int SyncBatchSize = 50;
    public const int EmbeddingDimensions = 768;
}
