namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 2 — Req 3
public sealed record GoldenDatasetEntry(
    string Query,
    string Expected_Page_Slug,
    string? Notes = null);

// Refs: FEAT-0060 Phase 3 — Req 4 (populated in Phase 3 #103)
public sealed record QueryResult(
    string Query,
    string Expected_Page_Slug,
    IReadOnlyList<RankedPage> RankedResults);

public sealed record RankedPage(
    string PageSlug,
    float Score,
    int Rank);
