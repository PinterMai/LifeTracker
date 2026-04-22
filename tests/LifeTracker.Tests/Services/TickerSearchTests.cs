using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Tests.Services;

public class TickerSearchTests
{
    private static readonly IReadOnlyList<TickerSymbol> Sample = new[]
    {
        new TickerSymbol("AAPL", "Apple Inc.", "NASDAQ"),
        new TickerSymbol("MSFT", "Microsoft Corporation", "NASDAQ"),
        new TickerSymbol("AMZN", "Amazon.com Inc.", "NASDAQ"),
        new TickerSymbol("GOOGL", "Alphabet Inc. Class A", "NASDAQ"),
        new TickerSymbol("APP", "AppLovin Corporation", "NASDAQ"),
        new TickerSymbol("BTCUSD", "Bitcoin", "Crypto"),
        new TickerSymbol("SAP", "SAP SE", "XETRA"),
    };

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        Assert.Empty(TickerSearch.Search(Sample, ""));
        Assert.Empty(TickerSearch.Search(Sample, null));
        Assert.Empty(TickerSearch.Search(Sample, "   "));
    }

    [Fact]
    public void Search_ExactSymbol_RanksFirst()
    {
        var result = TickerSearch.Search(Sample, "APP");
        Assert.Equal("APP", result[0].Symbol);
    }

    [Fact]
    public void Search_SymbolPrefix_BeatsNamePrefix()
    {
        // "APP" is exact so it wins, but "AAPL" starts with "AA"
        // and "APP" also starts with "AP" — prefix ranking check.
        var result = TickerSearch.Search(Sample, "AP");
        // Both AAPL and APP start with "AP". Symbol prefix score 80 applies to both.
        // Tiebreaker: shorter symbol first → APP (3) before AAPL (4).
        Assert.Equal("APP", result[0].Symbol);
        Assert.Equal("AAPL", result[1].Symbol);
    }

    [Fact]
    public void Search_NamePrefix_MatchesApple()
    {
        var result = TickerSearch.Search(Sample, "apple");
        // "Apple Inc." starts with "apple" → name prefix score 60.
        // AAPL is not a symbol prefix of "apple" → no symbol score.
        // AppLovin: "AppLovin" starts with "app" but not "apple" → won't match name-prefix for "apple".
        Assert.Contains(result, t => t.Symbol == "AAPL");
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var lower = TickerSearch.Search(Sample, "aapl");
        var upper = TickerSearch.Search(Sample, "AAPL");
        Assert.Equal(upper[0].Symbol, lower[0].Symbol);
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        // Query "A" matches many — limit should cap.
        var result = TickerSearch.Search(Sample, "A", limit: 2);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Search_ZeroLimit_ReturnsEmpty()
    {
        Assert.Empty(TickerSearch.Search(Sample, "APPL", limit: 0));
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(TickerSearch.Search(Sample, "XYZZZ"));
    }

    [Fact]
    public void Search_ThrowsOnNullSource()
    {
        Assert.Throws<ArgumentNullException>(() => TickerSearch.Search(null!, "A"));
    }

    [Fact]
    public void Search_NameContains_MatchesCorporation()
    {
        // "Corporation" appears in MSFT and APP names.
        var result = TickerSearch.Search(Sample, "Corporation");
        Assert.Contains(result, t => t.Symbol == "MSFT");
        Assert.Contains(result, t => t.Symbol == "APP");
    }
}
