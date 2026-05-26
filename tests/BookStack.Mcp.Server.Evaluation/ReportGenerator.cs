namespace BookStack.Mcp.Server.Evaluation.Tests;

// Refs: FEAT-0060 Phase 4 — Req 1
// Thin wrapper that delegates to MarkdownReportWriter and returns the report as a string.
public static class ReportGenerator
{
    public static Task<string> GenerateMarkdownReportAsync(EvaluationResult result)
        => MarkdownReportWriter.GenerateMarkdownReportAsync(result);
}
