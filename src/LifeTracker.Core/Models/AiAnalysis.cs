namespace LifeTracker.Core.Models;

/// <summary>
/// Structured result of an AI analysis call. The text is the free-form
/// analyst answer the caller shows to the user; the usage numbers are
/// surfaced so the user can watch their own API spend. Citations are
/// web sources the model pulled in via search grounding — empty when
/// the call ran ungrounded.
/// </summary>
public sealed record AiAnalysis(
    string Text,
    int InputTokens,
    int OutputTokens,
    string Model,
    IReadOnlyList<AiCitation>? Citations = null);

/// <summary>
/// Single web source Gemini cited during grounded generation.
/// </summary>
public sealed record AiCitation(string Title, string Url);
