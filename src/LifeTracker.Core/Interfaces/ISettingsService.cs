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
}
