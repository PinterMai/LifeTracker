using LifeTracker.Core.Models;

namespace LifeTracker.Core.Interfaces;

/// <summary>
/// Abstraction over whichever LLM provider is wired in. Implementations
/// are expected to talk directly to the provider's HTTP API — callers
/// should not know about keys, models, or transports.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Feedback on a single trade in the context of the user's history.
    /// </summary>
    Task<AiAnalysis> AnalyzeTradeAsync(
        Trade trade,
        IReadOnlyList<Trade> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan recent public posts from the given X/Twitter handles and
    /// extract ticker mentions with sentiment. Backed by Gemini +
    /// Google Search grounding — only indexed posts are visible, so
    /// coverage is partial. Returns an empty list if nothing was found
    /// or the grounding call didn't surface anything parseable.
    /// </summary>
    Task<IReadOnlyList<SignalCandidate>> ScanSignalsAsync(
        IReadOnlyList<string> handles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap round-trip to verify the stored API key works.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
