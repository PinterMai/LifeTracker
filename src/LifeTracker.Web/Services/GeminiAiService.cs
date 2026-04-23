using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;
using LifeTracker.Core.Services;

namespace LifeTracker.Web.Services;

/// <summary>
/// IAiService talking directly to Google's Gemini API. Gemini allows
/// API-key-in-query-string calls from any origin (CORS is open), which
/// makes it the most frictionless free provider for a pure Blazor WASM
/// app — no proxy, no special headers.
/// </summary>
public sealed class GeminiAiService : IAiService
{
    // Google's "latest" alias — always resolves to the current free-tier
    // flash model. Concrete versions like gemini-1.5-flash get deprecated
    // and cause 404 / 503 errors on freshly issued keys.
    public const string DefaultModel = "gemini-flash-latest";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;

    public GeminiAiService(HttpClient http, ISettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<AiAnalysis> AnalyzeTradeAsync(
        Trade trade,
        IReadOnlyList<Trade> history,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(history);

        var (apiKey, model) = await GetKeyAndModelAsync(cancellationToken).ConfigureAwait(false);
        var handles = await GetTrustedHandlesAsync(cancellationToken).ConfigureAwait(false);
        var prompt = TradePromptBuilder.BuildAnalyzePrompt(trade, history, trustedHandles: handles);

        // google_search = Gemini's built-in grounding tool. Enables live
        // web lookups and fills response.groundingMetadata.groundingChunks
        // with source URLs we surface in the UI.
        var tools = new[] { new GeminiTool(GoogleSearch: new object()) };

        var request = new GeminiRequest(
            SystemInstruction: new GeminiContent(new[] { new GeminiPart(TradePromptBuilder.SystemInstruction) }),
            Contents: new[] { new GeminiContent(new[] { new GeminiPart(prompt) }) },
            Tools: tools);

        var response = await PostAsync(apiKey, model, request, cancellationToken).ConfigureAwait(false);

        var candidate = response?.Candidates?.FirstOrDefault();
        // Flash models sometimes return multi-part answers; concatenate all
        // text parts so we don't silently drop the model's continuation.
        var text = candidate?.Content?.Parts is { Count: > 0 } parts
            ? string.Concat(parts.Select(p => p.Text ?? string.Empty)).Trim()
            : "(empty response)";

        var citations = ExtractCitations(candidate?.GroundingMetadata);

        return new AiAnalysis(
            Text: string.IsNullOrWhiteSpace(text) ? "(empty response)" : text,
            InputTokens: response?.UsageMetadata?.PromptTokenCount ?? 0,
            OutputTokens: response?.UsageMetadata?.CandidatesTokenCount ?? 0,
            Model: model,
            Citations: citations);
    }

    private async Task<IReadOnlyList<string>?> GetTrustedHandlesAsync(CancellationToken ct)
    {
        var raw = await _settings.GetAsync(SettingsKeys.TrustedXHandles, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        return raw
            .Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim().TrimStart('@'))
            .Where(h => h.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<AiCitation>? ExtractCitations(GeminiGroundingMetadata? meta)
    {
        if (meta?.GroundingChunks is not { Count: > 0 } chunks) return null;

        var list = new List<AiCitation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in chunks)
        {
            var web = c.Web;
            if (web is null) continue;
            var url = web.Uri ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!seen.Add(url)) continue;
            var title = string.IsNullOrWhiteSpace(web.Title) ? url : web.Title!;
            list.Add(new AiCitation(title, url));
        }
        return list.Count == 0 ? null : list;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (apiKey, model) = await GetKeyAndModelAsync(cancellationToken).ConfigureAwait(false);

            var request = new GeminiRequest(
                SystemInstruction: null,
                Contents: new[] { new GeminiContent(new[] { new GeminiPart("ping") }) });

            var response = await PostAsync(apiKey, model, request, cancellationToken).ConfigureAwait(false);
            return response?.Candidates is { Count: > 0 };
        }
        catch
        {
            return false;
        }
    }

    private async Task<(string apiKey, string model)> GetKeyAndModelAsync(CancellationToken ct)
    {
        var apiKey = await _settings.GetAsync(SettingsKeys.AiApiKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No AI API key configured. Open Settings and paste your Gemini API key.");
        }

        var model = await _settings.GetAsync(SettingsKeys.AiModel, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DefaultModel;
        }

        return (apiKey, model);
    }

    private async Task<GeminiResponse?> PostAsync(
        string apiKey,
        string model,
        GeminiRequest request,
        CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        using var httpResponse = await _http.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Gemini API returned {(int)httpResponse.StatusCode}: {body}");
        }

        return await httpResponse.Content
            .ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    // --- Wire types -------------------------------------------------

    private sealed record GeminiRequest(
        [property: JsonPropertyName("systemInstruction")]
        GeminiContent? SystemInstruction,
        [property: JsonPropertyName("contents")]
        IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("tools")]
        IReadOnlyList<GeminiTool>? Tools = null);

    // Gemini wire format uses snake_case field names inside tool objects.
    // google_search = built-in grounding with web search; the value is an
    // empty object per the v1beta spec.
    private sealed record GeminiTool(
        [property: JsonPropertyName("google_search")]
        object? GoogleSearch);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")]
        IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")]
        string? Text);

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")]
        IReadOnlyList<GeminiCandidate>? Candidates,
        [property: JsonPropertyName("usageMetadata")]
        GeminiUsage? UsageMetadata);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")]
        GeminiContent? Content,
        [property: JsonPropertyName("groundingMetadata")]
        GeminiGroundingMetadata? GroundingMetadata);

    private sealed record GeminiGroundingMetadata(
        [property: JsonPropertyName("groundingChunks")]
        IReadOnlyList<GeminiGroundingChunk>? GroundingChunks);

    private sealed record GeminiGroundingChunk(
        [property: JsonPropertyName("web")]
        GeminiWebSource? Web);

    private sealed record GeminiWebSource(
        [property: JsonPropertyName("uri")]
        string? Uri,
        [property: JsonPropertyName("title")]
        string? Title);

    private sealed record GeminiUsage(
        [property: JsonPropertyName("promptTokenCount")]
        int PromptTokenCount,
        [property: JsonPropertyName("candidatesTokenCount")]
        int CandidatesTokenCount);
}
