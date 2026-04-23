using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Web.Services;

/// <summary>
/// Loads <c>wwwroot/data/tickers.json</c> once on first use, keeps it in
/// memory for the lifetime of the SPA, and delegates ranking to
/// <see cref="TickerSearch"/>. Also folds in any tickers the Signals
/// scan has discovered on X (stored as JSON in ISettingsService under
/// <see cref="SettingsKeys.DiscoveredTickers"/>) so a symbol that the
/// AI pulled out of a tweet becomes autocomplete-visible immediately.
/// </summary>
public sealed class StaticTickerCatalog : ITickerCatalog
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<TickerSymbol>? _baseCatalog;
    private List<TickerSymbol> _discovered = new();
    private IReadOnlyList<TickerSymbol>? _merged;

    public StaticTickerCatalog(HttpClient http, ISettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<IReadOnlyList<TickerSymbol>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var catalog = await EnsureLoadedAsync(cancellationToken);
        return TickerSearch.Search(catalog, query, limit);
    }

    /// <summary>
    /// Persist a new ticker the scan discovered. Dedupes by symbol
    /// (case-insensitive) so repeated mentions don't balloon storage.
    /// Forces the next Search call to rebuild its merged view.
    /// </summary>
    public async Task AddDiscoveredAsync(
        TickerSymbol ticker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (string.IsNullOrWhiteSpace(ticker.Symbol)) return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedNoLockAsync(cancellationToken);

            // Skip if already in the base catalog or in discovered.
            bool alreadyKnown =
                _baseCatalog!.Any(t => t.Symbol.Equals(ticker.Symbol, StringComparison.OrdinalIgnoreCase))
                || _discovered.Any(t => t.Symbol.Equals(ticker.Symbol, StringComparison.OrdinalIgnoreCase));

            if (alreadyKnown) return;

            _discovered.Add(ticker);
            _merged = null; // force rebuild on next Search

            var json = JsonSerializer.Serialize(
                _discovered.Select(t => new TickerDto
                {
                    Symbol = t.Symbol,
                    Name = t.Name,
                    Exchange = t.Exchange
                }));
            await _settings.SetAsync(SettingsKeys.DiscoveredTickers, json, cancellationToken);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task<IReadOnlyList<TickerSymbol>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_merged is not null) return _merged;

        await _loadLock.WaitAsync(ct);
        try
        {
            await EnsureLoadedNoLockAsync(ct);
            _merged ??= _discovered.Count == 0
                ? _baseCatalog!
                : _baseCatalog!.Concat(_discovered).ToArray();
            return _merged;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task EnsureLoadedNoLockAsync(CancellationToken ct)
    {
        if (_baseCatalog is null)
        {
            var dtos = await _http.GetFromJsonAsync<TickerDto[]>(
                "data/tickers.json", ct);

            _baseCatalog = dtos is null
                ? Array.Empty<TickerSymbol>()
                : dtos
                    .Where(d => !string.IsNullOrWhiteSpace(d.Symbol))
                    .Select(d => new TickerSymbol(d.Symbol!, d.Name ?? string.Empty, d.Exchange ?? string.Empty))
                    .ToArray();

            // Hydrate discovered from settings. Bad JSON = start fresh.
            var raw = await _settings.GetAsync(SettingsKeys.DiscoveredTickers, ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<TickerDto[]>(raw);
                    if (saved is not null)
                    {
                        _discovered = saved
                            .Where(d => !string.IsNullOrWhiteSpace(d.Symbol))
                            .Select(d => new TickerSymbol(d.Symbol!, d.Name ?? string.Empty, d.Exchange ?? "X Scan"))
                            .ToList();
                    }
                }
                catch (JsonException) { _discovered = new(); }
            }
        }
    }

    private sealed class TickerDto
    {
        [JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("exchange")] public string? Exchange { get; set; }
    }
}
