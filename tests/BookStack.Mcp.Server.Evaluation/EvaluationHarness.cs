using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 2 — Req 3, 4
public sealed class EvaluationHarness
{
    private readonly IConfiguration _configuration;

    public EvaluationHarness()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Evaluation.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public async Task Configuration_LoadsRequiredSettings()
    {
        var baseUrl = _configuration["BookStackBaseUrl"];
        var apiToken = _configuration["ApiToken"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiToken))
        {
            Skip.Test(
                "BookStackBaseUrl and ApiToken must be set via environment variables or appsettings.Evaluation.json.");
        }

        await Assert.That(baseUrl).IsNotNull().And.IsNotEmpty();
        await Assert.That(apiToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task GoldenDataset_LoadsFromEmbeddedResource()
    {
        var dataset = LoadGoldenDataset();

        await Assert.That(dataset).IsNotEmpty();
    }

    // Refs: FEAT-0060 Phase 3 — Req 3
    // Enqueues a full vector index sync via the admin HTTP endpoint, then polls
    // GET /admin/status until pendingCount reaches 0.
    internal async Task TriggerFullSyncAndWaitAsync(CancellationToken cancellationToken = default)
    {
        var adminBaseUrl = _configuration["AdminBaseUrl"]
            ?? throw new InvalidOperationException(
                "AdminBaseUrl must be configured in appsettings.Evaluation.json or environment variables.");

        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(adminBaseUrl) };

        using var syncResponse = await http
            .PostAsync("/admin/sync", content: null, cancellationToken)
            .ConfigureAwait(false);
        syncResponse.EnsureSuccessStatusCode();

        // Poll until no pending tasks remain (sync complete).
        var pollInterval = TimeSpan.FromSeconds(5);
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);

            var status = await http
                .GetFromJsonAsync<AdminStatus>("/admin/status", cancellationToken)
                .ConfigureAwait(false);

            if (status?.PendingCount == 0)
            {
                break;
            }
        }
    }

    // Refs: FEAT-0060 Phase 3 — Req 4
    // Calls bookstack_semantic_search for each entry in the golden dataset and records ranked results.
    internal async Task<IReadOnlyList<QueryResult>> RunQueriesAsync(
        IReadOnlyList<GoldenDatasetEntry> dataset,
        CancellationToken cancellationToken = default)
    {
        var mcpBaseUrl = _configuration["McpBaseUrl"]
            ?? throw new InvalidOperationException(
                "McpBaseUrl must be configured in appsettings.Evaluation.json or environment variables.");
        var authToken = _configuration["McpAuthToken"];
        var topK = _configuration.GetValue<int?>("VectorSearch:TopK") ?? 5;

        using var mcpClient = new McpHttpClient(mcpBaseUrl, authToken);

        var results = new List<QueryResult>(dataset.Count);
        foreach (var entry in dataset)
        {
            var ranked = await mcpClient
                .CallSemanticSearchAsync(entry.Query, topK, cancellationToken)
                .ConfigureAwait(false);
            results.Add(new QueryResult(entry.Query, entry.Expected_Page_Slug, ranked));
        }

        return results.AsReadOnly();
    }

    internal static IReadOnlyList<GoldenDatasetEntry> LoadGoldenDataset()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("golden-dataset.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'golden-dataset.json' not found.");

        var entries = JsonSerializer.Deserialize<List<GoldenDatasetEntry>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize golden-dataset.json.");

        return entries.AsReadOnly();
    }

    private sealed record AdminStatus(int TotalPages, string? LastSyncTime, int PendingCount);
}

