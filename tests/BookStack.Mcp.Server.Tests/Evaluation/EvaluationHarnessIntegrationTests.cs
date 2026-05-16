using BookStack.Mcp.Server.Evaluation;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Evaluation;

// Refs: FEAT-0060 Phase 4 — Req 4 (integration test fixture)
// Tests the report writer and evaluation runner without requiring live endpoints.
public sealed class EvaluationReportTests
{
    [Test]
    public async Task ReportWriter_ProducesValidMarkdown()
    {
        var queryResults = MakeSampleResults();
        var result = EvaluationRunner.BuildResult(queryResults);

        using var stream = new MemoryStream();
        await MarkdownReportWriter.WriteAsync(result, stream).ConfigureAwait(false);

        var markdown = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        markdown.Should().Contain("# Semantic Search Quality Evaluation Report");
        markdown.Should().Contain("## Metrics");
        markdown.Should().Contain("## Score Distribution Histogram");
        markdown.Should().Contain("Recall@1");
        markdown.Should().Contain("Recall@3");
        markdown.Should().Contain("MRR");
        markdown.Should().Contain("| Correct | Incorrect |");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task EvaluationRunner_ComputesCorrectVerdicts()
    {
        // 2 queries: both correct at rank 1 → Recall@1 = 1.0 (PASS)
        var queryResults = MakeSampleResults();
        var result = EvaluationRunner.BuildResult(queryResults);

        result.RecallAt1.Should().BeApproximately(1.0f, precision: 0.01f);
        result.RecallAt3.Should().BeApproximately(1.0f, precision: 0.01f);
        result.MetricVerdicts.Should().AllSatisfy(v => v.Verdict.Should().Be("PASS"));
        result.OverallVerdict.Should().Be("not required");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task EvaluationRunner_DetectsFail()
    {
        // 1 query: incorrect result → Recall@1 = 0.0 (FAIL)
        var queryResults = new List<QueryResult>
        {
            new("q1", "expected-page", new List<RankedPage>
            {
                new("wrong-page", Score: 0.95f, Rank: 1),
            }.AsReadOnly()),
        }.AsReadOnly();

        var result = EvaluationRunner.BuildResult(queryResults);

        result.MetricVerdicts.Should().AllSatisfy(v => v.Verdict.Should().Be("FAIL"));
        result.OverallVerdict.Should().Be("Phase 2 required");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task EvaluationRunner_DetectsInvestigate()
    {
        // Data designed to produce at least one INVESTIGATE verdict
        // Recall@1: 1/2 = 0.5 (between pass 0.60 and investigate 0.45)
        var queryResults = new List<QueryResult>
        {
            new("q1", "page-a", new List<RankedPage>
            {
                new("page-a", Score: 0.95f, Rank: 1),
            }.AsReadOnly()),
            new("q2", "page-b", new List<RankedPage>
            {
                new("page-c", Score: 0.90f, Rank: 1),
                new("page-d", Score: 0.85f, Rank: 2),
                new("page-b", Score: 0.75f, Rank: 3),
            }.AsReadOnly()),
        }.AsReadOnly();

        var result = EvaluationRunner.BuildResult(queryResults);

        result.MetricVerdicts.Should().Contain(v => v.Verdict == "INVESTIGATE");
        result.OverallVerdict.Should().Be("investigate");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static IReadOnlyList<QueryResult> MakeSampleResults()
    {
        return new List<QueryResult>
        {
            new("q1", "page-a", new List<RankedPage>
            {
                new("page-a", Score: 0.95f, Rank: 1),
                new("page-b", Score: 0.85f, Rank: 2),
            }.AsReadOnly()),
            new("q2", "page-b", new List<RankedPage>
            {
                new("page-b", Score: 0.90f, Rank: 1),
                new("page-c", Score: 0.80f, Rank: 2),
            }.AsReadOnly()),
        }.AsReadOnly();
    }
}
