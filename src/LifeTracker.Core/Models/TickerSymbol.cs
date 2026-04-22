namespace LifeTracker.Core.Models;

/// <summary>
/// A single entry in the ticker autocomplete catalog. Exchange is
/// informational only — it helps disambiguate dual-listed names in
/// the dropdown (e.g. SAP on XETRA vs NYSE) but doesn't drive behaviour.
/// </summary>
public sealed record TickerSymbol(
    string Symbol,
    string Name,
    string Exchange);
