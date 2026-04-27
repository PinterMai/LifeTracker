namespace LifeTracker.Core.Interfaces;

/// <summary>
/// Minimal key/value bag for app settings. Persistence is up to the
/// implementation (localStorage in the browser, JSON file on desktop,
/// whatever). Keeping it string-typed avoids coupling Core to any
/// serializer.
/// </summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Which LLM provider to hit. Adding one is: new enum value, new
/// IAiService implementation, register it in DI.
/// </summary>
public enum AiProvider
{
    Gemini = 0,
    Claude = 1
}

/// <summary>
/// Well-known keys used across the app. Kept here so every consumer
/// spells them the same way.
/// </summary>
public static class SettingsKeys
{
    public const string AiProvider = "ai_provider";
    public const string AiApiKey = "ai_api_key";
    public const string AiModel = "ai_model";

    // Comma-separated list of X/Twitter handles (without @) the user
    // trusts for trade signal. Fed into the Gemini grounded prompt so
    // it searches what these accounts recently said about the ticker.
    public const string TrustedXHandles = "trusted_x_handles";

    // JSON array of { symbol, name, exchange } the Signals scan has
    // discovered. Merged into the static ticker catalog so a ticker
    // mentioned on X shows up in autocomplete even if it wasn't in
    // the pre-built NASDAQ+other-listed dataset.
    public const string DiscoveredTickers = "discovered_tickers";

    // ISO-8601 UTC timestamp of the last successful Signals scan.
    // Used to client-side-rate-limit the Scan button so an accidental
    // re-click doesn't burn the Gemini free-tier daily quota.
    public const string LastSignalsScanAt = "last_signals_scan_at";

    // Cached JSON array of the last successful Signals scan result.
    // Rehydrated on page load so the user sees the last scan without
    // paying for a new API call on every navigation.
    public const string LastSignalsResult = "last_signals_result";

    // Base URL of the Cloudflare Worker that runs the daily Gemini scan
    // server-side. When set, the Signals page reads from
    // <url>/signals/latest instead of calling Gemini from the browser.
    // Empty/null = legacy direct-from-browser mode.
    public const string SignalsBackendUrl = "signals_backend_url";
}
