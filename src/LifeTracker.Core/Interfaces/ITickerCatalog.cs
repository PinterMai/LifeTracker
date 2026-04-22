using LifeTracker.Core.Models;

namespace LifeTracker.Core.Interfaces;

/// <summary>
/// Autocomplete backend for the trade ticker input. Implementations are
/// expected to cache their source — the UI calls this on every keystroke.
/// The contract is intentionally query-by-string so we can swap a static
/// JSON file today for a remote API later without touching the UI.
/// </summary>
public interface ITickerCatalog
{
    /// <summary>
    /// Return the top <paramref name="limit"/> matches for
    /// <paramref name="query"/>, ordered by relevance. An empty or
    /// whitespace query returns an empty list — callers should hide
    /// the dropdown in that case.
    /// </summary>
    Task<IReadOnlyList<TickerSymbol>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
