using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Tests.Services;

public class TradePromptBuilderTests
{
    [Fact]
    public void BuildAnalyzePrompt_IncludesTradeFields()
    {
        var trade = new Trade
        {
            Id = 1,
            Ticker = "AAPL",
            Direction = Direction.Long,
            AmountInvested = 500m,
            OpenPrice = 180m,
            ClosePrice = 195m,
            Notes = "bounced off 180 support",
            OpenedAt = new DateTime(2026, 4, 15, 10, 30, 0)
        };

        var prompt = TradePromptBuilder.BuildAnalyzePrompt(trade, Array.Empty<Trade>());

        Assert.Contains("AAPL", prompt);
        Assert.Contains("Long", prompt);
        Assert.Contains("180", prompt);
        Assert.Contains("195", prompt);
        Assert.Contains("bounced off 180 support", prompt);
        Assert.Contains("2026-04-15", prompt);
    }

    [Fact]
    public void BuildAnalyzePrompt_EmptyHistory_AnnouncesFirstTrade()
    {
        var trade = new Trade { Id = 1, Ticker = "BTC" };

        var prompt = TradePromptBuilder.BuildAnalyzePrompt(trade, Array.Empty<Trade>());

        Assert.Contains("first logged trade", prompt);
    }

    [Fact]
    public void BuildAnalyzePrompt_ExcludesCurrentTradeFromHistory()
    {
        var current = new Trade
        {
            Id = 42,
            Ticker = "AAPL",
            OpenPrice = 100m,
            ClosePrice = 110m,
            Direction = Direction.Long,
            OpenedAt = new DateTime(2026, 4, 20)
        };

        var history = new List<Trade>
        {
            current, // same id — must be filtered out
            new() { Id = 1, Ticker = "TSLA", OpenPrice = 200m, ClosePrice = 210m, Direction = Direction.Long, OpenedAt = new DateTime(2026, 4, 10) }
        };

        var prompt = TradePromptBuilder.BuildAnalyzePrompt(current, history);

        // TSLA should appear in the history section; the current trade's id-42 line should not
        // appear twice. Easiest assertion: count "TSLA" once and "AAPL" exactly once
        // (the "== This trade ==" block) — if Id 42 leaked into history we'd see AAPL twice.
        Assert.Single(FindAll(prompt, "AAPL"));
        Assert.Single(FindAll(prompt, "TSLA"));
    }

    [Fact]
    public void BuildAnalyzePrompt_RespectsHistoryLimit()
    {
        var current = new Trade { Id = 999, Ticker = "NVDA" };

        var history = Enumerable.Range(1, 50)
            .Select(i => new Trade
            {
                Id = i,
                Ticker = $"SYM{i}",
                OpenPrice = 100m,
                ClosePrice = 105m,
                Direction = Direction.Long,
                OpenedAt = new DateTime(2026, 1, 1).AddDays(i)
            })
            .ToList();

        var prompt = TradePromptBuilder.BuildAnalyzePrompt(current, history, historyLimit: 5);

        // Newest 5 (SYM46..SYM50) must be in; older ones must not.
        Assert.Contains("SYM50", prompt);
        Assert.Contains("SYM46", prompt);
        Assert.DoesNotContain("SYM45", prompt);
        Assert.DoesNotContain("SYM1 ", prompt);
    }

    [Fact]
    public void SystemInstruction_IsNonEmptyAndMentionsJournal()
    {
        Assert.False(string.IsNullOrWhiteSpace(TradePromptBuilder.SystemInstruction));
        Assert.Contains("journal", TradePromptBuilder.SystemInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAnalyzePrompt_ThrowsOnNullArgs()
    {
        var trade = new Trade();
        Assert.Throws<ArgumentNullException>(() => TradePromptBuilder.BuildAnalyzePrompt(null!, Array.Empty<Trade>()));
        Assert.Throws<ArgumentNullException>(() => TradePromptBuilder.BuildAnalyzePrompt(trade, null!));
    }

    private static IEnumerable<int> FindAll(string haystack, string needle)
    {
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            yield return idx;
            idx += needle.Length;
        }
    }
}
