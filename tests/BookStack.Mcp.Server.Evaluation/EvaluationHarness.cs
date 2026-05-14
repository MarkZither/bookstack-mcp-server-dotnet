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

    // Placeholder — implemented in Phase 3 (#103)
    internal Task TriggerFullSyncAndWaitAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Implemented in Phase 3 (#103)");
    }

    // Placeholder — implemented in Phase 3 (#103)
    internal Task<IReadOnlyList<QueryResult>> RunQueriesAsync(
        IReadOnlyList<GoldenDatasetEntry> dataset,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Implemented in Phase 3 (#103)");
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
}
