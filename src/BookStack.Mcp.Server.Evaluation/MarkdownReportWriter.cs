using System.Text;

namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 4 — Req 1
public static class MarkdownReportWriter
{
    public static async Task WriteAsync(EvaluationResult result, Stream output)
    {
        using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync("# Semantic Search Quality Evaluation Report").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        await writer.WriteLineAsync($"**Generated**: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        // Overall verdict
        await writer.WriteLineAsync("## Overall Verdict").ConfigureAwait(false);
        await writer.WriteLineAsync($"**{result.OverallVerdict.ToUpperInvariant()}**").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        // Metrics table
        await writer.WriteLineAsync("## Metrics").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("| Metric | Value | Pass Threshold | Investigate | Verdict |")
            .ConfigureAwait(false);
        await writer.WriteLineAsync("|--------|-------|----------------|-------------|---------|").ConfigureAwait(false);

        foreach (var v in result.MetricVerdicts)
        {
            var valueStr = v.Value.ToString("F4");
            var passStr = v.PassThreshold.ToString("F2");
            var invStr = v.InvestigateThreshold.ToString("F2");
            var verdictStr = v.Verdict;

            await writer.WriteLineAsync(
                $"| {v.Name} | {valueStr} | ≥ {passStr} | ≥ {invStr} | **{verdictStr}** |")
                .ConfigureAwait(false);
        }

        await writer.WriteLineAsync().ConfigureAwait(false);

        // Score histogram
        await writer.WriteLineAsync("## Score Distribution Histogram").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("| Score Range | Correct | Incorrect |").ConfigureAwait(false);
        await writer.WriteLineAsync("|-------------|---------|-----------|").ConfigureAwait(false);

        foreach (var bucket in result.ScoreHistogram.CorrectBuckets.Keys)
        {
            var correct = result.ScoreHistogram.CorrectBuckets[bucket];
            var incorrect = result.ScoreHistogram.IncorrectBuckets[bucket];
            await writer.WriteLineAsync($"| {bucket} | {correct} | {incorrect} |").ConfigureAwait(false);
        }

        await writer.WriteLineAsync().ConfigureAwait(false);

        // Summary
        await writer.WriteLineAsync("## Summary").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync($"- **Queries evaluated**: {result.QueryResults.Count}").ConfigureAwait(false);
        await writer.WriteLineAsync($"- **Verdict**: {result.OverallVerdict}").ConfigureAwait(false);

        await writer.FlushAsync().ConfigureAwait(false);
    }
}
