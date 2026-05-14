using BookStack.Mcp.Server.Evaluation;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Evaluation;

// Refs: FEAT-0060 Phase 3 — Req 5 (MetricsCalculator unit tests)
public sealed class MetricsCalculatorTests
{
    // --- ComputeRecallAtK ---

    // T_RK_01: single result, exact hit at rank 1 → Recall@1 = 1.0
    [Test]
    public async Task ComputeRecallAtK_SingleHitAtRank1_Returns1()
    {
        var results = MakeResults([("q1", "page-a", ["page-a", "page-b", "page-c"])]);

        var recall = MetricsCalculator.ComputeRecallAtK(results, k: 1);

        await Assert.That(recall).IsEqualTo(1.0f);
    }

    // T_RK_02: expected page at rank 2, k=1 → miss
    [Test]
    public async Task ComputeRecallAtK_HitAtRank2_KEquals1_Returns0()
    {
        var results = MakeResults([("q1", "page-a", ["page-b", "page-a", "page-c"])]);

        var recall = MetricsCalculator.ComputeRecallAtK(results, k: 1);

        await Assert.That(recall).IsEqualTo(0.0f);
    }

    // T_RK_03: expected page at rank 2, k=3 → hit
    [Test]
    public async Task ComputeRecallAtK_HitAtRank2_KEquals3_Returns1()
    {
        var results = MakeResults([("q1", "page-a", ["page-b", "page-a", "page-c"])]);

        var recall = MetricsCalculator.ComputeRecallAtK(results, k: 3);

        await Assert.That(recall).IsEqualTo(1.0f);
    }

    // T_RK_04: 4 queries, 3 hits in top-1 → 0.75
    [Test]
    public async Task ComputeRecallAtK_ThreeOfFourHits_Returns075()
    {
        var results = MakeResults([
            ("q1", "page-a", ["page-a"]),
            ("q2", "page-b", ["page-b"]),
            ("q3", "page-c", ["page-c"]),
            ("q4", "page-d", ["page-x"]),
        ]);

        var recall = MetricsCalculator.ComputeRecallAtK(results, k: 1);

        recall.Should().BeApproximately(0.75f, precision: 0.001f);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // T_RK_05: empty results → 0
    [Test]
    public async Task ComputeRecallAtK_EmptyResults_Returns0()
    {
        var recall = MetricsCalculator.ComputeRecallAtK([], k: 3);

        await Assert.That(recall).IsEqualTo(0.0f);
    }

    // --- ComputeMRR ---

    // T_MRR_01: expected at rank 1 → MRR = 1.0
    [Test]
    public async Task ComputeMRR_HitAtRank1_Returns1()
    {
        var results = MakeResults([("q1", "page-a", ["page-a", "page-b"])]);

        var mrr = MetricsCalculator.ComputeMRR(results);

        await Assert.That(mrr).IsEqualTo(1.0f);
    }

    // T_MRR_02: expected at rank 2 → MRR = 0.5
    [Test]
    public async Task ComputeMRR_HitAtRank2_Returns05()
    {
        var results = MakeResults([("q1", "page-a", ["page-b", "page-a"])]);

        var mrr = MetricsCalculator.ComputeMRR(results);

        mrr.Should().BeApproximately(0.5f, precision: 0.001f);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // T_MRR_03: not in top-10 → MRR = 0.0
    [Test]
    public async Task ComputeMRR_NotInTop10_Returns0()
    {
        var slugs = Enumerable.Range(1, 10).Select(i => $"page-{i}").ToList();
        var results = MakeResults([("q1", "page-missing", slugs)]);

        var mrr = MetricsCalculator.ComputeMRR(results);

        await Assert.That(mrr).IsEqualTo(0.0f);
    }

    // T_MRR_04: two queries, ranks 1 and 2 → MRR = (1.0 + 0.5) / 2 = 0.75
    [Test]
    public async Task ComputeMRR_TwoQueries_Rank1And2_Returns075()
    {
        var results = MakeResults([
            ("q1", "page-a", ["page-a", "page-b"]),
            ("q2", "page-b", ["page-a", "page-b"]),
        ]);

        var mrr = MetricsCalculator.ComputeMRR(results);

        mrr.Should().BeApproximately(0.75f, precision: 0.001f);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // T_MRR_05: empty results → 0
    [Test]
    public async Task ComputeMRR_EmptyResults_Returns0()
    {
        var mrr = MetricsCalculator.ComputeMRR([]);

        await Assert.That(mrr).IsEqualTo(0.0f);
    }

    // --- ComputeScoreHistogram ---

    // T_HIST_01: correct hit at score 0.95 lands in bucket "0.9-1.0"
    [Test]
    public async Task ComputeScoreHistogram_CorrectHitAtHighScore_InTopBucket()
    {
        var results = new List<QueryResult>
        {
            new("q1", "page-a", new List<RankedPage>
            {
                new("page-a", Score: 0.95f, Rank: 1),
                new("page-b", Score: 0.55f, Rank: 2),
            }.AsReadOnly()),
        };

        var histogram = MetricsCalculator.ComputeScoreHistogram(results);

        histogram.CorrectBuckets["0.9-1.0"].Should().Be(1);
        histogram.IncorrectBuckets["0.5-0.6"].Should().Be(1);
        histogram.CorrectBuckets["0.5-0.6"].Should().Be(0);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // T_HIST_02: empty results → all buckets zero
    [Test]
    public async Task ComputeScoreHistogram_EmptyResults_AllZero()
    {
        var histogram = MetricsCalculator.ComputeScoreHistogram([]);

        histogram.CorrectBuckets.Values.Sum().Should().Be(0);
        histogram.IncorrectBuckets.Values.Sum().Should().Be(0);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // T_HIST_03: 10 buckets present in both dictionaries
    [Test]
    public async Task ComputeScoreHistogram_ReturnsTenBuckets()
    {
        var histogram = MetricsCalculator.ComputeScoreHistogram([]);

        await Assert.That(histogram.CorrectBuckets.Count).IsEqualTo(10);
        await Assert.That(histogram.IncorrectBuckets.Count).IsEqualTo(10);
    }

    // --- helpers ---

    private static IReadOnlyList<QueryResult> MakeResults(
        IEnumerable<(string Query, string ExpectedSlug, IEnumerable<string> Slugs)> fixtures)
    {
        return fixtures.Select((f, _) =>
        {
            var ranked = f.Slugs
                .Select((slug, i) => new RankedPage(slug, Score: 1.0f - i * 0.1f, Rank: i + 1))
                .ToList()
                .AsReadOnly();
            return new QueryResult(f.Query, f.ExpectedSlug, ranked);
        }).ToList().AsReadOnly();
    }
}
