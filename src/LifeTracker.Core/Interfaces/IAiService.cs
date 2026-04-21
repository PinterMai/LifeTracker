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
    /// Cheap round-trip to verify the stored API key works.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
