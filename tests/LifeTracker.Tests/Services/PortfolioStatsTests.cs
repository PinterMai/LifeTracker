using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Tests.Services;

public class PortfolioStatsTests
{
    private static Trade Long(string ticker, decimal amount, decimal open, decimal close,
        DateTime? openedAt = null) => new()
    {
        Ticker = ticker,
        Direction = Direction.Long,
        AmountInvested = amount,
        OpenPrice = open,
        ClosePrice = close,
        OpenedAt = openedAt ?? new DateTime(2026, 1, 1),
    };

    private static Trade Short_(string ticker, decimal amount, decimal open, decimal close,
        DateTime? openedAt = null) => new()
    {
        Ticker = ticker,
        Direction = Direction.Short,
        AmountInvested = amount,
        OpenPrice = open,
        ClosePrice = close,
        OpenedAt = openedAt ?? new DateTime(2026, 1, 1),
    };

    [Fact]
    public void ComputeKpi_NoTrades_ReturnsZeros()
    {
        var kpi = PortfolioStats.ComputeKpi(Array.Empty<Trade>());

        Assert.Equal(0, kpi.TotalTrades);
        Assert.Equal(0, kpi.WinRatePercent);
        Assert.Equal(0m, kpi.TotalProfitLoss);
        Assert.Equal(0, kpi.ProfitFactor);
    }

    [Fact]
    public void ComputeKpi_AllWinners_ProfitFactorIsZero()
    {
        // Convention: profit factor is undefined when there are no
        // losses, surfaced as 0 so the UI can render "—" instead of
        // dividing by zero.
        var trades = new[] { Long("AAPL", 1000, 100, 110) };

        var kpi = PortfolioStats.ComputeKpi(trades);

        Assert.Equal(0, kpi.Losses);
        Assert.Equal(0, kpi.ProfitFactor);
        Assert.Equal(100m, kpi.TotalProfitLoss); // 1000 * 10%
    }

    [Fact]
    public void ComputeKpi_MixedTrades_AggregatesCorrectly()
    {
        var trades = new[]
        {
            Long("AAPL", 1000, 100, 110),  // +10% = +$100
            Long("TSLA", 500, 200, 180),   // -10% = -$50
            Short_("MSFT", 800, 400, 380), // +5%  = +$40
        };

        var kpi = PortfolioStats.ComputeKpi(trades);

        Assert.Equal(3, kpi.TotalTrades);
        Assert.Equal(2, kpi.Wins);
        Assert.Equal(1, kpi.Losses);
        Assert.Equal(0, kpi.BreakEven);
        Assert.Equal(90m, kpi.TotalProfitLoss);
        Assert.Equal(30m, kpi.AverageProfitLoss);
        Assert.Equal(100m, kpi.BestTradeProfitLoss);
        Assert.Equal(-50m, kpi.WorstTradeProfitLoss);
        // grossWins = 140, grossLosses = 50, factor = 2.8
        Assert.Equal(2.8, kpi.ProfitFactor, 1);
    }

    [Fact]
    public void ComputeEquityCurve_BucketsByDay()
    {
        var trades = new[]
        {
            Long("AAPL", 1000, 100, 110, new DateTime(2026, 1, 1)),
            Long("MSFT", 500,  200, 220, new DateTime(2026, 1, 1)), // same day
            Long("TSLA", 1000, 100, 90,  new DateTime(2026, 1, 3)),
        };

        var curve = PortfolioStats.ComputeEquityCurve(trades);

        // Day 1 sums two trades into one point, day 2 has nothing, day 3
        // is its own point. We don't fill missing days — the chart
        // connects the dots.
        Assert.Equal(2, curve.Count);
        Assert.Equal(new DateTime(2026, 1, 1), curve[0].Date);
        Assert.Equal(150m, curve[0].CumulativeProfitLoss); // 100 + 50
        Assert.Equal(new DateTime(2026, 1, 3), curve[1].Date);
        Assert.Equal(50m, curve[1].CumulativeProfitLoss); // 150 - 100
    }

    [Fact]
    public void ComputePerSymbol_SortsByTotalDescending()
    {
        var trades = new[]
        {
            Long("AAPL", 1000, 100, 110), // +$100
            Long("AAPL", 500,  100, 90),  // -$50  → AAPL net +$50
            Long("MSFT", 1000, 100, 120), // +$200
            Long("TSLA", 500,  100, 95),  // -$25
        };

        var stats = PortfolioStats.ComputePerSymbol(trades);

        Assert.Equal(3, stats.Count);
        // MSFT first (200), AAPL second (50), TSLA last (-25).
        Assert.Equal("MSFT", stats[0].Ticker);
        Assert.Equal("AAPL", stats[1].Ticker);
        Assert.Equal("TSLA", stats[2].Ticker);

        var aapl = stats[1];
        Assert.Equal(2, aapl.TradeCount);
        Assert.Equal(1, aapl.Wins);
        Assert.Equal(50m, aapl.TotalProfitLoss);
        Assert.Equal(50.0, aapl.WinRatePercent);
    }

    [Fact]
    public void ComputePerSymbol_TickerIsCaseInsensitive()
    {
        var trades = new[]
        {
            Long("aapl", 1000, 100, 110),
            Long("AAPL", 500,  100, 105),
        };

        var stats = PortfolioStats.ComputePerSymbol(trades);

        var single = Assert.Single(stats);
        Assert.Equal("AAPL", single.Ticker);
        Assert.Equal(2, single.TradeCount);
    }

    [Fact]
    public void ComputeMonthly_GroupsByYearMonth()
    {
        var trades = new[]
        {
            Long("AAPL", 1000, 100, 110, new DateTime(2026, 1, 5)),
            Long("MSFT", 1000, 100, 120, new DateTime(2026, 1, 28)),
            Long("TSLA", 1000, 100, 95,  new DateTime(2026, 2, 10)),
        };

        var months = PortfolioStats.ComputeMonthly(trades);

        Assert.Equal(2, months.Count);
        Assert.Equal((2026, 1, 300m, 2),
            (months[0].Year, months[0].Month, months[0].ProfitLoss, months[0].TradeCount));
        Assert.Equal((2026, 2, -50m, 1),
            (months[1].Year, months[1].Month, months[1].ProfitLoss, months[1].TradeCount));
    }
}
