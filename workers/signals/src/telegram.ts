// Optional Telegram push: when the daily cron finds at least one
// actionable recommendation (Long or Short), the worker fires a single
// Bot API message so the user gets a phone notification without ever
// opening the PWA. Watch/Avoid recs are interesting but not urgent —
// keeping them out of pushes prevents notification fatigue.

import type { CachedScan } from './types';

// Mirrors the C# RecommendationAction enum and the integer codes in
// types.ts. Kept local so callers don't have to import a numeric magic
// constant.
const ACTION_WATCH = 0;
const ACTION_LONG = 1;
const ACTION_SHORT = 2;
const ACTION_AVOID = 3;

export interface TelegramOpts {
  botToken: string;
  chatId: string;
}

/** Returns true when notify-worthy. Currently: any Long or Short rec. */
export function shouldNotify(cached: CachedScan): boolean {
  return cached.recommendations.some(
    (r) => r.action === ACTION_LONG || r.action === ACTION_SHORT,
  );
}

/**
 * Posts a formatted summary to Telegram. Errors are caught and logged
 * but never thrown — a Telegram outage must not poison the KV write.
 */
export async function sendTelegramAlert(
  cached: CachedScan,
  opts: TelegramOpts,
): Promise<void> {
  try {
    const text = formatMessage(cached);
    const url = `https://api.telegram.org/bot${opts.botToken}/sendMessage`;

    const resp = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        chat_id: opts.chatId,
        text,
        parse_mode: 'HTML',
        disable_web_page_preview: true,
      }),
    });

    if (!resp.ok) {
      const body = await resp.text();
      console.error(`Telegram API ${resp.status}: ${body}`);
    }
  } catch (err) {
    console.error('Telegram push failed:', err);
  }
}

function formatMessage(cached: CachedScan): string {
  const lines: string[] = [];
  lines.push('<b>LifeTracker daily scan</b>');

  // Strip seconds + drop "T" so the timestamp is human-friendly in
  // the Telegram preview.
  const when = cached.scannedAtUtc.replace('T', ' ').slice(0, 16) + ' UTC';
  lines.push(`<i>${when} · ${cached.handleCount} handles</i>`);
  lines.push('');

  // Sort: Long first, then Short, then Watch, then Avoid. The user's
  // eye lands on actionable picks before noise.
  const sorted = [...cached.recommendations].sort(
    (a, b) => actionWeight(a.action) - actionWeight(b.action),
  );

  for (const rec of sorted) {
    const icon = actionIcon(rec.action);
    const label = actionLabel(rec.action);
    const handles =
      rec.supportingHandles.length > 0
        ? ` <i>(${rec.supportingHandles.slice(0, 3).join(', ')})</i>`
        : '';
    lines.push(
      `${icon} <b>${label}</b> ${escapeHtml(rec.ticker)} — ${escapeHtml(rec.reasoning)}${handles}`,
    );
  }

  if (sorted.length === 0) {
    lines.push('No recommendations today.');
  }

  return lines.join('\n');
}

function actionIcon(a: number): string {
  switch (a) {
    case ACTION_LONG:
      return '🟢';
    case ACTION_SHORT:
      return '🔴';
    case ACTION_WATCH:
      return '👀';
    case ACTION_AVOID:
      return '⚠️';
    default:
      return '•';
  }
}

function actionLabel(a: number): string {
  switch (a) {
    case ACTION_LONG:
      return 'LONG';
    case ACTION_SHORT:
      return 'SHORT';
    case ACTION_WATCH:
      return 'WATCH';
    case ACTION_AVOID:
      return 'AVOID';
    default:
      return '?';
  }
}

function actionWeight(a: number): number {
  switch (a) {
    case ACTION_LONG:
      return 0;
    case ACTION_SHORT:
      return 1;
    case ACTION_WATCH:
      return 2;
    case ACTION_AVOID:
      return 3;
    default:
      return 4;
  }
}

// Telegram's HTML mode chokes on un-escaped <, >, &. Tickers and
// reasoning come from the model, so they're not trusted to be safe.
function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}
