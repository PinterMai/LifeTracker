namespace LifeTracker.Core.Models;

/// <summary>
/// Structured result of an AI analysis call. The text is the free-form
/// analyst answer the caller shows to the user; the usage numbers are
/// surfaced so the user can watch their own API spend.
/// </summary>
public sealed record AiAnalysis(
    string Text,
    int InputTokens,
    int OutputTokens,
    string Model);
