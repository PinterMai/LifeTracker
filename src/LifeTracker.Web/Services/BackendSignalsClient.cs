using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;

namespace LifeTracker.Web.Services;

/// <summary>
/// Reads the latest cached signal scan from the LifeTracker Cloudflare
/// Worker. The worker runs the daily cron with its own Gemini key and
/// stores the result in KV; the frontend just reads, so the user's
/// browser never burns Gemini quota for the scan.
/// </summary>
public sealed class BackendSignalsClient
{
    // Worker emits camelCase JSON; the C# records use PascalCase property
    // names. Single options instance reused per request.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;

    public BackendSignalsClient(HttpClient http, ISettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    /// <summary>
    /// Whether the user has configured a backend URL. Useful to gate UI
    /// without doing a network round-trip.
    /// </summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var url = await _settings.GetAsync(SettingsKeys.SignalsBackendUrl, ct);
        return !string.IsNullOrWhiteSpace(url);
    }

    /// <summary>
    /// Fetches the latest scan from the worker's <c>/signals/latest</c>
    /// endpoint. Returns <c>null</c> when no backend URL is configured
    /// or the worker hasn't produced its first scan yet (404).
    /// </summary>
    public async Task<BackendSignalsSnapshot?> GetLatestAsync(CancellationToken ct = default)
    {
        var baseUrl = await _settings.GetAsync(SettingsKeys.SignalsBackendUrl, ct);
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        var url = baseUrl.TrimEnd('/') + "/signals/latest";

        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Backend returned {(int)resp.StatusCode}: {body}");
        }

        var dto = await resp.Content
            .ReadFromJsonAsync<CachedScanDto>(JsonOpts, ct)
            .ConfigureAwait(false);

        if (dto is null) return null;

        var result = new SignalsScanResult(
            dto.Recommendations ?? Array.Empty<Recommendation>(),
            dto.RawSignals ?? Array.Empty<SignalCandidate>());

        return new BackendSignalsSnapshot(
            Result: result,
            ScannedAtUtc: dto.ScannedAtUtc,
            HandleCount: dto.HandleCount,
            Model: dto.Model);
    }

    /// <summary>Wire shape coming from the worker. Mirrors workers/signals/src/types.ts CachedScan.</summary>
    private sealed record CachedScanDto(
        IReadOnlyList<Recommendation>? Recommendations,
        IReadOnlyList<SignalCandidate>? RawSignals,
        DateTime ScannedAtUtc,
        int HandleCount,
        string? Model);
}

/// <summary>
/// Snapshot of one worker scan: the result plus the metadata the cron
/// stamped on it (when, how many handles, which model). Surfaced in the
/// UI so the user can see "scanned 3h ago" without trusting the local
/// clock.
/// </summary>
public sealed record BackendSignalsSnapshot(
    SignalsScanResult Result,
    DateTime ScannedAtUtc,
    int HandleCount,
    string? Model);
