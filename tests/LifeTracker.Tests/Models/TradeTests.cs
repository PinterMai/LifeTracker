using LifeTracker.Core.Models;

namespace LifeTracker.Tests.Models;

public class TradeTests
{
    [Fact]
    public void ProfitLossPercent_LongWinner_ReturnsPositive()
    {
        var trade = new Trade
        {
            Direction = Direction.Long,
            OpenPrice = 100m,
            ClosePrice = 110m
        };

        Assert.Equal(10m, trade.ProfitLossPercent);
    }

    [Fact]
    public void ProfitLossPercent_LongLoser_ReturnsNegative()
    {
        var trade = new Trade
        {
            Direction = Direction.Long,
            OpenPrice = 100m,
            ClosePrice = 90m
        };

        Assert.Equal(-10m, trade.ProfitLossPercent);
    }

    [Fact]
    public void ProfitLossPercent_ShortWinner_ReturnsPositive()
    {
        var trade = new Trade
        {
            Direction = Direction.Short,
            OpenPrice = 100m,
            ClosePrice = 90m
        };

        Assert.Equal(10m, trade.ProfitLossPercent);
    }

    [Fact]
    public void ProfitLossPercent_ShortLoser_ReturnsNegative()
    {
        var trade = new Trade
        {
            Direction = Direction.Short,
            OpenPrice = 100m,
            ClosePrice = 110m
        };

        Assert.Equal(-10m, trade.ProfitLossPercent);
    }

    [Fact]
    public void ProfitLossPercent_BreakEven_ReturnsZero()
    {
        var trade = new Trade
        {
            Direction = Direction.Long,
            OpenPrice = 100m,
            ClosePrice = 100m
        };

        Assert.Equal(0m, trade.ProfitLossPercent);
    }

    [Fact]
    public void ProfitLossPercent_OpenPriceZero_ReturnsZero()
    {
        // Guards against divide-by-zero when a trade is partially entered
        // (e.g., user typed ticker but not yet open price).
        var trade = new Trade
        {
            Direction = Direction.Long,
            OpenPrice = 0m,
            ClosePrice = 100m
        };

        Assert.Equal(0m, trade.ProfitLossPercent);
    }

    [Theory]
    [InlineData(100, 125, 25)]    // +25%
    [InlineData(200, 150, -25)]   // -25%
    [InlineData(1, 2, 100)]       // +100%
    [InlineData(50.50, 55.55, 10.0)] // +10% with decimals
    public void ProfitLossPercent_Long_CalculatesCorrectly(double open, double close, double expected)
    {
        var trade = new Trade
        {
            Direction = Direction.Long,
            OpenPrice = (decimal)open,
            ClosePrice = (decimal)close
        };

        Assert.Equal((decimal)expected, trade.ProfitLossPercent, precision: 2);
    }

    [Fact]
    public void NewTrade_DefaultsOpenedAtToNow()
    {
        var before = DateTime.Now;
        var trade = new Trade();
        var after = DateTime.Now;

        Assert.InRange(trade.OpenedAt, before, after);
    }

    [Fact]
    public void NewTrade_DefaultsStringsToEmpty()
    {
        var trade = new Trade();

        Assert.Equal(string.Empty, trade.Ticker);
        Assert.Equal(string.Empty, trade.Notes);
    }
}
