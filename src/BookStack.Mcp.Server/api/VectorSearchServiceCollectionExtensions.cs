using Azure;
using Azure.AI.OpenAI;
using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using BookStack.Mcp.Server.Data.Postgres;
using BookStack.Mcp.Server.Data.Sqlite;
using BookStack.Mcp.Server.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Api;

public static class VectorSearchServiceCollectionExtensions
{
    public static IServiceCollection AddVectorSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VectorSearchOptions>(
            configuration.GetSection("VectorSearch"));

        var options = configuration
            .GetSection("VectorSearch")
            .Get<VectorSearchOptions>() ?? new VectorSearchOptions();

        if (!options.Enabled)
        {
            // Register no-op implementations so that SemanticSearchToolHandler
            // can still be resolved by the MCP assembly scan. The handler checks
            // options.Enabled before calling either dependency.
            services.AddSingleton<IVectorStore, DisabledVectorStore>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, DisabledEmbeddingGenerator>();
            return services;
        }

        RegisterEmbeddingGenerator(services, options);
        RegisterVectorStore(services, options, configuration);

        services.AddSingleton<VectorIndexSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<VectorIndexSyncService>());

        return services;
    }

    private static void RegisterEmbeddingGenerator(
        IServiceCollection services,
        VectorSearchOptions options)
    {
        var provider = options.EmbeddingProvider;

        if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<VectorSearchOptions>>().Value.AzureOpenAI;
                var client = new AzureOpenAIClient(
                    new Uri(opts.Endpoint),
                    new AzureKeyCredential(opts.ApiKey));
                return client.GetEmbeddingClient(opts.DeploymentName).AsIEmbeddingGenerator();
            });
        }
        else
        {
            // Default: Ollama
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<VectorSearchOptions>>().Value.Ollama;
                return new OllamaEmbeddingGenerator(opts.BaseUrl, opts.Model);
            });
        }
    }

    private static void RegisterVectorStore(
        IServiceCollection services,
        VectorSearchOptions options,
        IConfiguration configuration)
    {
        var db = options.Database;
        var connectionString = configuration.GetConnectionString("VectorDb")
            ?? "Data Source=bookstack-vectors.db";

        if (string.Equals(db, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            services.AddPostgresVectorStore(connectionString);
        }
        else
        {
            // Default: Sqlite
            services.AddSqliteVectorStore(connectionString);
        }
    }

    private sealed class DisabledVectorStore : IVectorStore
    {
        public Task UpsertAsync(VectorPageEntry entry, ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(ReadOnlyMemory<float> queryVector, int topN, float minScore, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public Task DeleteAsync(int pageId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public Task<string?> GetContentHashAsync(int pageId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public Task<DateTimeOffset?> GetLastSyncAtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task SetLastSyncAtAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class DisabledEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata Metadata { get; } = new("disabled", null, null, null);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Vector search is disabled.");

        public TService? GetService<TService>(object? key = null) where TService : class
            => null;

        object? IEmbeddingGenerator.GetService(Type serviceType, object? key)
            => null;

        public void Dispose() { }
    }
}
