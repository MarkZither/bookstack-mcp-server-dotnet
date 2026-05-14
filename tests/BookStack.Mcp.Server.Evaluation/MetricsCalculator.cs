namespace BookStack.Mcp.Server.Evaluation;

// Refs: FEAT-0060 Phase 3 — Req 5
public static class MetricsCalculator
{
    private const int MaxRankForMrr = 10;

    /// <summary>
    /// Fraction of queries where the expected page appears in the top-K results.
    /// </summary>
    public static float ComputeRecallAtK(IReadOnlyList<QueryResult> results, int k)
    {
        if (results.Count == 0)
        {
            return 0f;
        }

        var hits = 0;
        foreach (var result in results)
        {
            var top = result.RankedResults.Count < k ? result.RankedResults.Count : k;
            for (var i = 0; i < top; i++)
            {
                if (result.RankedResults[i].PageSlug == result.Expected_Page_Slug)
                {
                    hits++;
                    break;
                }
            }
        }

        return (float)hits / results.Count;
    }

    /// <summary>
    /// Mean Reciprocal Rank — mean of 1/rank for the first correct result in top-10 (0 if not found).
    /// </summary>
    public static float ComputeMRR(IReadOnlyList<QueryResult> results)
    {
        if (results.Count == 0)
        {
            return 0f;
        }

        var sum = 0.0;
        foreach (var result in results)
        {
            var limit = Math.Min(result.RankedResults.Count, MaxRankForMrr);
            for (var i = 0; i < limit; i++)
            {
                if (result.RankedResults[i].PageSlug == result.Expected_Page_Slug)
                {
                    sum += 1.0 / (i + 1);
                    break;
                }
            }
        }

        return (float)(sum / results.Count);
    }

    /// <summary>
    /// Score histogram bucketed into 0.1-wide bands for correct vs. incorrect hits.
    /// </summary>
    public static ScoreHistogram ComputeScoreHistogram(IReadOnlyList<QueryResult> results)
    {
        var correct = new int[10];
        var incorrect = new int[10];

        foreach (var result in results)
        {
            foreach (var page in result.RankedResults)
            {
                var bucket = Math.Min((int)(page.Score * 10), 9);
                if (page.PageSlug == result.Expected_Page_Slug)
                {
                    correct[bucket]++;
                }
                else
                {
                    incorrect[bucket]++;
                }
            }
        }

        var labels = new string[10];
        for (var i = 0; i < 10; i++)
        {
            labels[i] = $"{i * 0.1:F1}-{(i + 1) * 0.1:F1}";
        }

        var correctDict = new Dictionary<string, int>(10);
        var incorrectDict = new Dictionary<string, int>(10);
        for (var i = 0; i < 10; i++)
        {
            correctDict[labels[i]] = correct[i];
            incorrectDict[labels[i]] = incorrect[i];
        }

        return new ScoreHistogram(correctDict, incorrectDict);
    }
}
