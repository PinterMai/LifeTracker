// Wire shape returned to the LifeTracker PWA. Keys are camelCase so
// the C# client can deserialize with the default JsonNamingPolicy.CamelCase.
//
// Sentiment / Action are numeric to match the C# enums:
//   sentiment:  0=Unknown, 1=Bullish, 2=Bearish, 3=Neutral
//   action:     0=Watch,   1=Long,    2=Short,   3=Avoid

export interface SignalCandidate {
  handle: string;
  ticker: string;
  sentiment: number;
  quote: string;
  sourceUrl: string | null;
}

export interface Recommendation {
  ticker: string;
  action: number;
  reasoning: string;
  supportingHandles: string[];
}

export interface SignalsScanResult {
  recommendations: Recommendation[];
  rawSignals: SignalCandidate[];
}

/** Stored in KV. Adds metadata around the raw scan result. */
export interface CachedScan extends SignalsScanResult {
  scannedAtUtc: string; // ISO-8601
  handleCount: number;
  model: string;
}
