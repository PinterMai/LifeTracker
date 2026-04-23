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
