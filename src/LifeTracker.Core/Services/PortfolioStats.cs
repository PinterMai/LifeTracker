using LifeTracker.Core.Models;

namespace LifeTracker.Core.Services;

/// <summary>
/// Aggregates a batch of trades into the numbers the Stats page needs:
/// KPIs at the top, an equity curve, per-symbol breakdown, monthly P/L.
/// Intentionally pure (no DI, no DB) so it runs the same in xUnit and
/// in the browser, and so the Stats page can recompute on every render
/// without touching IndexedDB more than once.
/// </summary>
public static class PortfolioStats
{
    /// <summary>Headline numbers for the top of the Stats page.</summary>
    public sealed record KpiSummary(
        int TotalTrades,
        int Wins,
        int Losses,
        int BreakEven,
        double WinRatePercent,
        decimal TotalProfitLoss,
        decimal AverageProfitLoss,
        decimal BestTradeProfitLoss,
        decimal WorstTradeProfitLoss,
        double ProfitFactor);

    /// <summary>One point on the cumulative-dollar equity curve.</summary>
    public sealed record EquityPoint(DateTime Date, decimal CumulativeProfitLoss);

    /// <summary>Per-ticker rollup for the symbol table.</summary>
    public sealed record SymbolStats(
        string Ticker,
        int TradeCount,
        int Wins,
        decimal TotalProfitLoss,
        double WinRatePercent);

    /// <summary>Per-month rollup for the bar chart.</summary>
    public sealed record MonthlyStats(int Year, int Month, decimal ProfitLoss, int TradeCount);

    public static KpiSummary ComputeKpi(IEnumerable<Trade> trades)
    {
        // Materialise once so multiple LINQ scans don't re-iterate the
        // (potentially deferred) source. The caller's IEnumerable could
        // be an EF query — touching it twice would round-trip the DB.
        var list = trades as IReadOnlyList<Trade> ?? trades.ToList();

        if (list.Count == 0)
        {
            return new KpiSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        int wins = 0, losses = 0, breakEven = 0;
        decimal totalPl = 0;
        decimal best = decimal.MinValue;
        decimal worst = decimal.MaxValue;
        decimal grossWins = 0;
        decimal grossLosses = 0;

        foreach (var t in list)
        {
            var pl = AbsoluteProfitLoss(t);
            totalPl += pl;
            if (pl > best) best = pl;
            if (pl < worst) worst = pl;

            if (pl > 0) { wins++; grossWins += pl; }
            else if (pl < 0) { losses++; grossLosses += -pl; }
            else breakEven++;
        }

        var winRate = list.Count > 0 ? 100.0 * wins / list.Count : 0;
        var avgPl = totalPl / list.Count;

        // Profit factor = gross winning $ / gross losing $. Standard
        // convention: undefined when there are no losses, so we surface
        // it as 0 rather than dividing by zero — the UI shows a "—".
        var profitFactor = grossLosses > 0
            ? (double)(grossWins / grossLosses)
            : 0;

        return new KpiSummary(
            TotalTrades: list.Count,
            Wins: wins,
            Losses: losses,
            BreakEven: breakEven,
            WinRatePercent: winRate,
            TotalProfitLoss: totalPl,
            AverageProfitLoss: avgPl,
            BestTradeProfitLoss: best == decimal.MinValue ? 0 : best,
            WorstTradeProfitLoss: worst == decimal.MaxValue ? 0 : worst,
            ProfitFactor: profitFactor);
    }

    public static IReadOnlyList<EquityPoint> ComputeEquityCurve(IEnumerable<Trade> trades)
    {
        // Sort chronologically. We bucket by Date (no time component) so
        // multiple trades closed on the same day show up as one step.
        // That matches how a daytrader thinks about the curve more than
        // hour-by-hour ticks would.
        var ordered = trades
            .OrderBy(t => t.OpenedAt)
            .GroupBy(t => t.OpenedAt.Date)
            .OrderBy(g => g.Key);

        var points = new List<EquityPoint>();
        decimal running = 0;
        foreach (var day in ordered)
        {
            running += day.Sum(AbsoluteProfitLoss);
            points.Add(new EquityPoint(day.Key, running));
        }
        return points;
    }

    public static IReadOnlyList<SymbolStats> ComputePerSymbol(IEnumerable<Trade> trades)
    {
        return trades
            .GroupBy(t => t.Ticker, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var totalPl = g.Sum(AbsoluteProfitLoss);
                var wins = g.Count(t => AbsoluteProfitLoss(t) > 0);
                var winRate = g.Count() > 0 ? 100.0 * wins / g.Count() : 0;
                return new SymbolStats(
                    Ticker: g.Key.ToUpperInvariant(),
                    TradeCount: g.Count(),
                    Wins: wins,
                    TotalProfitLoss: totalPl,
                    WinRatePercent: winRate);
            })
            .OrderByDescending(s => s.TotalProfitLoss)
            .ToList();
    }

    public static IReadOnlyList<MonthlyStats> ComputeMonthly(IEnumerable<Trade> trades)
    {
        return trades
            .GroupBy(t => new { t.OpenedAt.Year, t.OpenedAt.Month })
            .Select(g => new MonthlyStats(
                Year: g.Key.Year,
                Month: g.Key.Month,
                ProfitLoss: g.Sum(AbsoluteProfitLoss),
                TradeCount: g.Count()))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();
    }

    /// <summary>
    /// Translates Trade.ProfitLossPercent (a percentage on OpenPrice)
    /// into actual dollar P/L on the AmountInvested. The model never
    /// stored the dollar version because the percentage is the lossless
    /// canonical form, but charting wants money on the y-axis.
    /// </summary>
    private static decimal AbsoluteProfitLoss(Trade t) =>
        t.AmountInvested * (t.ProfitLossPercent / 100m);
}
