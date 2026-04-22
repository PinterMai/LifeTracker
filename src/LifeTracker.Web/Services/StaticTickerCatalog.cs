using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Web.Services;

/// <summary>
/// Loads <c>wwwroot/data/tickers.json</c> once on first use, keeps it in
/// memory for the lifetime of the SPA, and delegates ranking to
/// <see cref="TickerSearch"/>. The file is ~1 MB gzipped and the browser
/// caches it aggressively, so the upfront cost is paid once per visit.
/// </summary>
public sealed class StaticTickerCatalog : ITickerCatalog
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<TickerSymbol>? _catalog;

    public StaticTickerCatalog(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<TickerSymbol>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var catalog = await EnsureLoadedAsync(cancellationToken);
        return TickerSearch.Search(catalog, query, limit);
    }

    private async Task<IReadOnlyList<TickerSymbol>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_catalog is not null)
            {
                return _catalog;
            }

            // System.Text.Json needs lowercase property names to match the
            // JSON we emit from update-tickers.ps1. The DTO mirrors
            // TickerSymbol but exists separately because records with
            // primary constructors + STJ source-gen play awkwardly in WASM.
            var dtos = await _http.GetFromJsonAsync<TickerDto[]>(
                "data/tickers.json", ct);

            _catalog = dtos is null
                ? Array.Empty<TickerSymbol>()
                : dtos
                    .Where(d => !string.IsNullOrWhiteSpace(d.Symbol))
                    .Select(d => new TickerSymbol(d.Symbol!, d.Name ?? string.Empty, d.Exchange ?? string.Empty))
                    .ToArray();

            return _catalog;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private sealed class TickerDto
    {
        [JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("exchange")] public string? Exchange { get; set; }
    }
}
