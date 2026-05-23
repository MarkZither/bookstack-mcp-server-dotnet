using Microsoft.Extensions.Configuration;

namespace BookStack.Mcp.Server.Evaluation.Tests;

// Refs: FEAT-0060 Phase 4 — Req 2, 3
// Full end-to-end evaluation: seed → sync → query → metrics → report.
// Requires a running BookStack + MCP server. Skipped when config is not populated.
public sealed class FullEvaluationTest
{
    private static readonly IConfiguration _config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.Evaluation.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

    [Test]
    public async Task RunFullEvaluationAndWriteReport()
    {
        var mcpBaseUrl = _config["McpBaseUrl"] ?? string.Empty;

        // Check reachability; skip if the server is not actually up.
        var reachable = await IsReachableAsync(mcpBaseUrl).ConfigureAwait(false);
        Skip.When(!reachable,
            "MCP server not reachable at McpBaseUrl — start the dev stack to run the evaluation.");

        var harness = new EvaluationHarness();

        // Step 1: trigger full vector sync and wait for completion.
        await harness.TriggerFullSyncAndWaitAsync().ConfigureAwait(false);

        // Step 2: load golden dataset and run queries.
        var dataset = EvaluationHarness.LoadGoldenDataset();
        var queryResults = await harness.RunQueriesAsync(dataset).ConfigureAwait(false);

        // Step 3: compute metrics and build result.
        var evaluationResult = EvaluationRunner.BuildResult(queryResults);

        // Step 4: generate markdown report.
        var report = await ReportGenerator.GenerateMarkdownReportAsync(evaluationResult)
            .ConfigureAwait(false);

        // Step 5: write report to docs/features/semantic-search-chunking/evaluation-report.md
        var repoRoot = FindRepoRoot();
        if (repoRoot is not null)
        {
            var reportPath = Path.Combine(
                repoRoot,
                "docs",
                "features",
                "semantic-search-chunking",
                "evaluation-report.md");

            await File.WriteAllTextAsync(reportPath, report, System.Text.Encoding.UTF8)
                .ConfigureAwait(false);

            Console.WriteLine($"Evaluation report written to: {reportPath}");
        }

        Console.WriteLine(report);

        // Step 6: assert quality gate — all metrics must be at least INVESTIGATE.
        var failedMetrics = evaluationResult.MetricVerdicts
            .Where(v => v.Verdict == "FAIL")
            .ToList();

        var message = failedMetrics.Count > 0
            ? $"Evaluation gate failed. Overall: {evaluationResult.OverallVerdict}. " +
              $"Failed metrics: {string.Join(", ", failedMetrics.Select(v => $"{v.Name}={v.Value:F4}"))}"
            : string.Empty;

        await Assert.That(failedMetrics.Count).IsEqualTo(0).Because(message);
    }

    private static async Task<bool> IsReachableAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(
            Path.GetDirectoryName(typeof(FullEvaluationTest).Assembly.Location)!);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BookStack.Mcp.Server.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
