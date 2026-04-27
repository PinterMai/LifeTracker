// TypeScript port of LifeTracker.Web/Services/GeminiAiService.cs ScanSignalsAsync.
// Identical prompt + parser shape so the worker output is byte-compatible
// with what the C# client expects to deserialize.
//
// Why duplicate instead of share: the C# version runs in WASM with the
// user's per-browser key; this version runs server-side with the worker's
// shared key. Different runtime, different host, same logic.

import type {
  Recommendation,
  SignalCandidate,
  SignalsScanResult,
} from './types';

const BASE_URL = 'https://generativelanguage.googleapis.com/v1beta/models';

export const DEFAULT_MODEL = 'gemini-flash-latest';

export interface ScanOptions {
  apiKey: string;
  model?: string;
}

/**
 * Calls Gemini once with grounded Google Search and parses the
 * pipe-delimited dual-section output into recommendations + raw signals.
 * Throws on HTTP errors so the cron handler can log status codes.
 */
export async function scanSignals(
  handles: string[],
  opts: ScanOptions,
): Promise<SignalsScanResult> {
  if (handles.length === 0) {
    return { recommendations: [], rawSignals: [] };
  }

  const model = opts.model || DEFAULT_MODEL;
  const handleList = handles
    .map((h) => '@' + h.replace(/^@/, '').trim())
    .join(', ');

  const body = {
    systemInstruction: {
      parts: [
        {
          text:
            'You are a disciplined market scanner. You return only the ' +
            'requested SIGNAL and REC lines or NO_SIGNALS. Never fabricate.',
        },
      ],
    },
    contents: [{ parts: [{ text: buildPrompt(handleList) }] }],
    tools: [{ google_search: {} }],
  };

  const url = `${BASE_URL}/${model}:generateContent?key=${encodeURIComponent(opts.apiKey)}`;
  const resp = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

  if (!resp.ok) {
    const errBody = await resp.text();
    throw new Error(`Gemini API ${resp.status}: ${errBody}`);
  }

  const data = (await resp.json()) as GeminiResponse;
  const parts = data?.candidates?.[0]?.content?.parts ?? [];
  const text = parts.map((p) => p.text ?? '').join('');

  if (!text || /NO_SIGNALS/i.test(text)) {
    return { recommendations: [], rawSignals: [] };
  }

  const rawSignals = parseSignalLines(text);
  const recommendations = parseRecLines(text, rawSignals);
  return { recommendations, rawSignals };
}

function buildPrompt(handleList: string): string {
  // Kept verbatim from the C# version. Any tweak here must be mirrored
  // in src/LifeTracker.Web/Services/GeminiAiService.cs so direct-call
  // and worker-call outputs stay parseable by the same regex.
  return [
    `You are scanning what these X/Twitter accounts recently posted about US equities or crypto: ${handleList}.`,
    '',
    'Using Google Search, look up recent posts (last 7 days) from each ' +
      'account that mention a specific ticker (e.g. $AAPL, $NVDA, BTC). ' +
      'Prefer `site:x.com @handle` queries.',
    '',
    'Output TWO sections, in this exact order, nothing else:',
    '',
    '[SIGNALS]',
    'One line per verified mention, in this EXACT format:',
    'SIGNAL: <handle> | <TICKER> | <bullish|bearish|neutral> | <short quote, max 140 chars, no pipes> | <source url>',
    '',
    '[RECOMMENDATIONS]',
    'Up to 5 lines, ranked by your conviction, in this EXACT format:',
    'REC: <TICKER> | <long|short|watch|avoid> | <reasoning, max 200 chars, no pipes> | <handle1,handle2,...>',
    '',
    'Rules:',
    '- Ticker must be uppercase letters only (optional .A/.B class suffix allowed).',
    '- A REC must reference a ticker that appears in [SIGNALS]. Do NOT invent tickers.',
    '- Action = long if dominant sentiment is bullish, short if bearish, watch if mixed/weak, avoid if a strong warning was posted.',
    '- Reasoning must summarize what the handles actually said — no your-own opinion, no boilerplate.',
    '- Prefer tickers where multiple handles agree. Single-handle picks are fine if conviction is clearly high.',
    "- Never fabricate quotes or URLs. If you can't find a post, skip the handle.",
    '- If no mentions at all, output exactly: NO_SIGNALS (and skip both sections).',
    '- No prose, no markdown, no commentary outside the SIGNAL/REC lines.',
  ].join('\n');
}

const SIGNAL_LINE_RX =
  /^\s*SIGNAL:\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*(bullish|bearish|neutral)\s*\|\s*([^|]+?)\s*\|\s*(\S+)\s*$/gim;

const REC_LINE_RX =
  /^\s*REC:\s*([^|]+?)\s*\|\s*(long|short|watch|avoid)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*$/gim;

function parseSignalLines(text: string): SignalCandidate[] {
  const list: SignalCandidate[] = [];
  SIGNAL_LINE_RX.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = SIGNAL_LINE_RX.exec(text)) !== null) {
    const handle = m[1].replace(/^@/, '').trim();
    const ticker = m[2].replace(/^\$/, '').trim().toUpperCase();
    const sentiment = m[3].toLowerCase();
    const quote = m[4].trim().replace(/^"|"$/g, '');
    const url = m[5].trim();
    if (!handle || !ticker) continue;
    list.push({
      handle,
      ticker,
      sentiment: sentimentToEnum(sentiment),
      quote,
      sourceUrl: url || null,
    });
  }
  return list;
}

function parseRecLines(
  text: string,
  signals: SignalCandidate[],
): Recommendation[] {
  // Drop any REC whose ticker wasn't actually grounded in a SIGNAL line.
  // Mirrors the C# anti-hallucination guard.
  const grounded = new Set(signals.map((s) => s.ticker.toUpperCase()));

  const list: Recommendation[] = [];
  REC_LINE_RX.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = REC_LINE_RX.exec(text)) !== null) {
    const ticker = m[1].replace(/^\$/, '').trim().toUpperCase();
    if (!ticker) continue;
    if (grounded.size > 0 && !grounded.has(ticker)) continue;

    const action = m[2].toLowerCase();
    const reasoning = m[3].trim().replace(/^"|"$/g, '');
    const handles = Array.from(
      new Set(
        m[4]
          .split(/[,;]/)
          .map((h) => h.trim().replace(/^@/, ''))
          .filter((h) => h.length > 0),
      ),
    );

    list.push({
      ticker,
      action: actionToEnum(action),
      reasoning,
      supportingHandles: handles,
    });
  }
  return list;
}

function sentimentToEnum(s: string): number {
  switch (s) {
    case 'bullish':
      return 1;
    case 'bearish':
      return 2;
    case 'neutral':
      return 3;
    default:
      return 0;
  }
}

function actionToEnum(a: string): number {
  switch (a) {
    case 'long':
      return 1;
    case 'short':
      return 2;
    case 'avoid':
      return 3;
    default:
      return 0;
  }
}

// --- Wire types (subset of Gemini response we care about) ----------------

interface GeminiResponse {
  candidates?: Array<{
    content?: {
      parts?: Array<{ text?: string }>;
    };
  }>;
}
