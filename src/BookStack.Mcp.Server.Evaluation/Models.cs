namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 2 — Req 3
public sealed record GoldenDatasetEntry(
    string Query,
    string Expected_Page_Slug,
    string? Notes = null);

// Refs: FEAT-0060 Phase 3 — Req 4
public sealed record QueryResult(
    string Query,
    string Expected_Page_Slug,
    IReadOnlyList<RankedPage> RankedResults);

public sealed record RankedPage(
    string PageSlug,
    float Score,
    int Rank);

// Refs: FEAT-0060 Phase 3 — Req 5
public sealed record ScoreHistogram(
    IReadOnlyDictionary<string, int> CorrectBuckets,
    IReadOnlyDictionary<string, int> IncorrectBuckets);

// Refs: FEAT-0060 Phase 4 — Req 1, 2
public sealed record MetricVerdict(
    string Name,
    float Value,
    string Verdict,
    float PassThreshold,
    float InvestigateThreshold);

// Refs: FEAT-0060 Phase 4 — Req 1, 2
public sealed record EvaluationResult(
    float RecallAt1,
    float RecallAt3,
    float Mrr,
    ScoreHistogram ScoreHistogram,
    IReadOnlyList<MetricVerdict> MetricVerdicts,
    string OverallVerdict,
    IReadOnlyList<QueryResult> QueryResults);
