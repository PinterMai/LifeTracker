using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Tests.Services;

public class CsvTradeParserTests
{
    private const string MinimalHeader =
        "Ticker,Direction,AmountInvested,OpenPrice,ClosePrice";

    [Fact]
    public void Parse_EmptyInput_ReturnsHeaderError()
    {
        var result = CsvTradeParser.Parse("");

        Assert.True(result.HasFatalErrors);
        Assert.Contains("empty", result.HeaderErrors[0], StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Parse_MissingRequiredColumn_ReportsHeaderError()
    {
        var csv = "Ticker,Direction,AmountInvested,OpenPrice\nAAPL,Long,1000,150";

        var result = CsvTradeParser.Parse(csv);

        Assert.True(result.HasFatalErrors);
        Assert.Contains(result.HeaderErrors,
            e => e.Contains("ClosePrice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MinimalValidRow_ProducesTrade()
    {
        var csv = $"{MinimalHeader}\nAAPL,Long,1000,150.50,165.20";

        var result = CsvTradeParser.Parse(csv);

        Assert.False(result.HasFatalErrors);
        var trade = Assert.Single(result.Trades);
        Assert.Equal("AAPL", trade.Ticker);
        Assert.Equal(Direction.Long, trade.Direction);
        Assert.Equal(1000m, trade.AmountInvested);
        Assert.Equal(150.50m, trade.OpenPrice);
        Assert.Equal(165.20m, trade.ClosePrice);
    }

    [Fact]
    public void Parse_LowercasedDirection_IsAccepted()
    {
        // Real broker CSVs love SHOUTING and lowercasing both, so we
        // make Direction case-insensitive instead of forcing the user
        // to massage their export.
        var csv = $"{MinimalHeader}\nTSLA,short,500,250,230";

        var result = CsvTradeParser.Parse(csv);

        var trade = Assert.Single(result.Trades);
        Assert.Equal(Direction.Short, trade.Direction);
    }

    [Fact]
    public void Parse_TickerIsUppercased()
    {
        var csv = $"{MinimalHeader}\naapl,Long,100,10,20";

        var result = CsvTradeParser.Parse(csv);

        Assert.Equal("AAPL", Assert.Single(result.Trades).Ticker);
    }

    [Fact]
    public void Parse_NotesWithEmbeddedComma_StaysIntactWhenQuoted()
    {
        var csv = $"{MinimalHeader},Notes\n" +
                  "AAPL,Long,1000,150,165,\"bounced, then ran\"";

        var result = CsvTradeParser.Parse(csv);

        var trade = Assert.Single(result.Trades);
        Assert.Equal("bounced, then ran", trade.Notes);
    }

    [Fact]
    public void Parse_OpenedAtIso_IsParsed()
    {
        var csv = $"{MinimalHeader},OpenedAt\n" +
                  "AAPL,Long,1000,150,165,2026-01-15";

        var result = CsvTradeParser.Parse(csv);

        Assert.Equal(new DateTime(2026, 1, 15), Assert.Single(result.Trades).OpenedAt);
    }

    [Fact]
    public void Parse_OpenedAtMissing_FallsBackToToday()
    {
        var csv = $"{MinimalHeader}\nAAPL,Long,1000,150,165";

        var result = CsvTradeParser.Parse(csv);

        // We just check the date part — time-of-day is whatever the
        // model defaults to and not what we're asserting.
        Assert.Equal(DateTime.Today.Date, Assert.Single(result.Trades).OpenedAt.Date);
    }

    [Fact]
    public void Parse_CommaDecimalSeparator_IsAccepted()
    {
        // EU Excel exports often use "150,50" instead of "150.50". We
        // tolerate both because our user is in Slovenia and might paste
        // numbers either way.
        var csv = $"{MinimalHeader}\nAAPL,Long,\"1000,00\",\"150,50\",\"165,20\"";

        var result = CsvTradeParser.Parse(csv);

        var trade = Assert.Single(result.Trades);
        Assert.Equal(150.50m, trade.OpenPrice);
        Assert.Equal(165.20m, trade.ClosePrice);
    }

    [Fact]
    public void Parse_BadDirection_RecordsRowError()
    {
        var csv = $"{MinimalHeader}\nAAPL,Sideways,1000,150,165";

        var result = CsvTradeParser.Parse(csv);

        var error = Assert.Single(result.Errors);
        Assert.Contains("Sideways", error.Error!);
        Assert.Empty(result.Trades);
    }

    [Fact]
    public void Parse_BadNumber_RecordsRowErrorButNeighborSurvives()
    {
        var csv = $"{MinimalHeader}\nAAPL,Long,abc,150,165\nMSFT,Long,500,300,310";

        var result = CsvTradeParser.Parse(csv);

        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.Errors);
        var ok = Assert.Single(result.Trades);
        Assert.Equal("MSFT", ok.Ticker);
    }

    [Fact]
    public void Parse_BlankLines_AreSkipped()
    {
        var csv = $"\n\n{MinimalHeader}\n\nAAPL,Long,1000,150,165\n\nMSFT,Long,500,300,310\n";

        var result = CsvTradeParser.Parse(csv);

        Assert.Equal(2, result.Trades.Count());
    }

    [Fact]
    public void Parse_ColumnOrderShuffled_StillMapsByHeader()
    {
        var csv = "Direction,Ticker,Notes,ClosePrice,OpenPrice,AmountInvested\n" +
                  "Long,AAPL,note,165,150,1000";

        var result = CsvTradeParser.Parse(csv);

        var trade = Assert.Single(result.Trades);
        Assert.Equal("AAPL", trade.Ticker);
        Assert.Equal(150m, trade.OpenPrice);
        Assert.Equal(165m, trade.ClosePrice);
        Assert.Equal("note", trade.Notes);
    }

    [Fact]
    public void Parse_CrlfLineEndings_HandledLikeLf()
    {
        var csv = $"{MinimalHeader}\r\nAAPL,Long,1000,150,165\r\nMSFT,Long,500,300,310\r\n";

        var result = CsvTradeParser.Parse(csv);

        Assert.Equal(2, result.Trades.Count());
    }
}
