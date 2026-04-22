using LifeTracker.Core.Models;

namespace LifeTracker.Core.Services;

/// <summary>
/// Pure in-memory ticker search. No I/O, no framework deps — the whole
/// ranking algorithm lives here so it can be unit-tested and reused by
/// any ITickerCatalog implementation (static JSON, remote API, cached DB).
/// </summary>
public static class TickerSearch
{
    /// <summary>
    /// Rank <paramref name="source"/> against <paramref name="query"/>
    /// and return the top <paramref name="limit"/> matches. Scoring:
    /// exact symbol > symbol prefix > name prefix > symbol contains >
    /// name contains. Ties broken by shorter symbol first, then
    /// alphabetical.
    /// </summary>
    public static IReadOnlyList<TickerSymbol> Search(
        IReadOnlyList<TickerSymbol> source,
        string? query,
        int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<TickerSymbol>();
        }

        var q = query.Trim();

        return source
            .Select(t => (ticker: t, score: Score(t, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.ticker.Symbol.Length)
            .ThenBy(x => x.ticker.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => x.ticker)
            .ToList();
    }

    private static int Score(TickerSymbol t, string q)
    {
        if (t.Symbol.Equals(q, StringComparison.OrdinalIgnoreCase))
            return 100;
        if (t.Symbol.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 80;
        if (t.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 60;
        if (t.Symbol.Contains(q, StringComparison.OrdinalIgnoreCase))
            return 40;
        if (t.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            return 20;
        return 0;
    }
}
