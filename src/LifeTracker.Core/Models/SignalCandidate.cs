namespace LifeTracker.Core.Models;

/// <summary>
/// A single ticker mention the AI found while scanning the user's
/// trusted X/Twitter handles. Not a trade signal in the compliance
/// sense — just "here's what @handle said about $TICKER, decide for
/// yourself". The UI surfaces these as cards the user can dismiss or
/// turn into a new trade.
/// </summary>
public sealed record SignalCandidate(
    string Handle,
    string Ticker,
    SignalSentiment Sentiment,
    string Quote,
    string? SourceUrl);

/// <summary>
/// Coarse sentiment bucket. The UI colors cards by this so the user
/// can skim the scan at a glance. Unknown is the honest default when
/// the AI couldn't read the tone.
/// </summary>
public enum SignalSentiment
{
    Unknown = 0,
    Bullish = 1,
    Bearish = 2,
    Neutral = 3
}

/// <summary>
/// Aggregated, opinionated read on a single ticker after looking at
/// what every trusted handle said this week. This is the "should I
/// look at this today?" output the user actually wants — not raw
/// mentions but a 1-sentence verdict with the supporting handles.
/// </summary>
public sealed record Recommendation(
    string Ticker,
    RecommendationAction Action,
    string Reasoning,
    IReadOnlyList<string> SupportingHandles);

/// <summary>
/// What the AI thinks the user should consider doing with the ticker.
/// Watch is the honest fallback when handles are mixed or vague —
/// we'd rather under-call than fake conviction.
/// </summary>
public enum RecommendationAction
{
    Watch = 0,
    Long = 1,
    Short = 2,
    Avoid = 3
}

/// <summary>
/// Combined result of one Signals scan: raw per-handle mentions plus
/// the AI's aggregated recommendations across all handles. Wrapping
/// both lets a single Gemini call power both UI sections.
/// </summary>
public sealed record SignalsScanResult(
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<SignalCandidate> RawSignals)
{
    public static SignalsScanResult Empty { get; } =
        new(Array.Empty<Recommendation>(), Array.Empty<SignalCandidate>());
}
