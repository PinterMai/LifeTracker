using System.Globalization;
using System.Text;
using LifeTracker.Core.Models;

namespace LifeTracker.Core.Services;

/// <summary>
/// Turns a Trade + history into a single text prompt for the LLM.
/// Pure — no I/O, no framework deps, fully unit-testable.
/// Lives in Core because the prompt itself is domain logic; the HTTP
/// transport (Gemini, Claude, …) is the interchangeable part.
/// </summary>
public static class TradePromptBuilder
{
    /// <summary>
    /// Instruction prepended to every request. Keeps the model's tone
    /// and scope consistent regardless of caller.
    /// </summary>
    public const string SystemInstruction =
        "You are a trading journal analyst. The user is a retail trader "
        + "journaling their own trades to find leaks in their process. "
        + "Be direct, specific, and evidence-based. Never give financial advice "
        + "or predict future prices. Focus on: setup classification, position "
        + "sizing sanity, emotional tells in the notes, and one concrete, "
        + "actionable improvement grounded in the trader's own history. "
        + "When Google Search is available, use it to check recent headlines "
        + "about the ticker (earnings, guidance, catalysts) and what the "
        + "listed trusted X/Twitter accounts recently said about it — cite "
        + "sources when you quote them. Respond in plain English under "
        + "250 words. No bullet lists unless the user asked for them.";

    /// <summary>
    /// Build the user-facing prompt describing a single trade and up to
    /// <paramref name="historyLimit"/> prior trades for context. If the
    /// caller supplies <paramref name="trustedHandles"/>, the prompt
    /// nudges the model to prefer those accounts when researching the
    /// ticker — Gemini's Google Search tool does the actual fetching.
    /// </summary>
    public static string BuildAnalyzePrompt(
        Trade trade,
        IReadOnlyList<Trade> history,
        int historyLimit = 30,
        IReadOnlyList<string>? trustedHandles = null)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(history);

        var sb = new StringBuilder();
        sb.AppendLine("Analyze this trade in the context of the user's history.");
        sb.AppendLine();
        sb.AppendLine("== This trade ==");
        AppendTrade(sb, trade);

        var relevant = history
            .Where(t => t.Id != trade.Id)
            .OrderByDescending(t => t.OpenedAt)
            .Take(historyLimit)
            .ToList();

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"== Last {relevant.Count} trades (newest first) ==");

        if (relevant.Count == 0)
        {
            sb.AppendLine("(no prior trades — this is the user's first logged trade)");
        }
        else
        {
            foreach (var t in relevant)
            {
                AppendTradeOneLine(sb, t);
            }
        }

        if (trustedHandles is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("== Trusted X accounts the trader follows ==");
            sb.AppendLine(
                "When researching the ticker, prefer what these accounts said "
                + "recently (search e.g. `site:x.com @handle TICKER`). Do not "
                + "invent quotes — only cite if you actually found a post.");
            foreach (var h in trustedHandles)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  @{h}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Tell the user:");
        sb.AppendLine("1. How this trade fits their pattern (win/loss profile, ticker, direction).");
        sb.AppendLine("2. Any recent news or sentiment about the ticker that's relevant to the setup.");
        sb.AppendLine("3. One concrete improvement grounded in their own history.");
        sb.AppendLine("4. A red or green flag you see in the notes, if anything jumps out.");

        return sb.ToString();
    }

    private static void AppendTrade(StringBuilder sb, Trade t)
    {
        var culture = CultureInfo.InvariantCulture;
        sb.AppendLine(culture, $"Ticker: {t.Ticker}");
        sb.AppendLine(culture, $"Direction: {t.Direction}");
        sb.AppendLine(culture, $"Amount invested: {t.AmountInvested:0.##}");
        sb.AppendLine(culture, $"Open: {t.OpenPrice:0.######} -> Close: {t.ClosePrice:0.######}");
        sb.AppendLine(culture, $"P/L: {t.ProfitLossPercent:+0.00;-0.00;0}%");
        sb.AppendLine(culture, $"Opened at: {t.OpenedAt:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrWhiteSpace(t.Notes))
        {
            sb.AppendLine(culture, $"Notes: {t.Notes}");
        }
    }

    private static void AppendTradeOneLine(StringBuilder sb, Trade t)
    {
        var culture = CultureInfo.InvariantCulture;
        sb.AppendLine(
            culture,
            $"- {t.OpenedAt:yyyy-MM-dd} {t.Ticker} {t.Direction} "
            + $"{t.OpenPrice:0.##}->{t.ClosePrice:0.##} "
            + $"{t.ProfitLossPercent:+0.00;-0.00;0}%");
    }
}
