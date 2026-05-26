namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 4 — Req 1, 2
public static class EvaluationRunner
{
    private static readonly (float Pass, float Investigate, string Name)[] _metricThresholds =
    [
        (0.60f, 0.45f, "Recall@1"),
        (0.75f, 0.60f, "Recall@3"),
        (0.65f, 0.50f, "MRR"),
    ];

    public static EvaluationResult BuildResult(IReadOnlyList<QueryResult> queryResults)
    {
        var recallAt1 = MetricsCalculator.ComputeRecallAtK(queryResults, k: 1);
        var recallAt3 = MetricsCalculator.ComputeRecallAtK(queryResults, k: 3);
        var mrr = MetricsCalculator.ComputeMRR(queryResults);
        var histogram = MetricsCalculator.ComputeScoreHistogram(queryResults);

        var verdicts = new List<MetricVerdict>(3)
        {
            BuildMetricVerdict(_metricThresholds[0], recallAt1),
            BuildMetricVerdict(_metricThresholds[1], recallAt3),
            BuildMetricVerdict(_metricThresholds[2], mrr),
        };

        var overall = ComputeOverallVerdict(verdicts);

        var latencies = queryResults.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();
        var p50 = latencies.Length > 0 ? latencies[(int)(latencies.Length * 0.50)] : 0L;
        var p95 = latencies.Length > 0 ? latencies[(int)(latencies.Length * 0.95)] : 0L;

        return new EvaluationResult(
            recallAt1,
            recallAt3,
            mrr,
            histogram,
            verdicts.AsReadOnly(),
            overall,
            queryResults,
            p50,
            p95);
    }

    private static MetricVerdict BuildMetricVerdict(
        (float Pass, float Investigate, string Name) thresholds,
        float value)
    {
        var verdict = value >= thresholds.Pass
            ? "PASS"
            : value >= thresholds.Investigate
                ? "INVESTIGATE"
                : "FAIL";

        return new MetricVerdict(
            thresholds.Name,
            value,
            verdict,
            thresholds.Pass,
            thresholds.Investigate);
    }

    private static string ComputeOverallVerdict(IReadOnlyList<MetricVerdict> verdicts)
    {
        if (verdicts.Any(v => v.Verdict == "FAIL"))
        {
            return "Phase 2 required";
        }

        if (verdicts.Any(v => v.Verdict == "INVESTIGATE"))
        {
            return "investigate";
        }

        return "not required";
    }
}
