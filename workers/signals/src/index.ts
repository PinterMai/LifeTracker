// Worker entry point.
//
// Two surfaces:
//   - fetch():     GET /signals/latest   -> latest cached scan from KV
//                  GET /health           -> "ok"
//                  POST /signals/scan?key=<SCAN_TRIGGER_SECRET> -> force scan
//   - scheduled(): runs the daily cron (see wrangler.toml triggers)
//
// All HTTP responses include CORS headers so the deployed PWA on
// pages.github.io can read them straight from the browser.

import { DEFAULT_MODEL, scanSignals } from './gemini';
import { sendTelegramAlert, shouldNotify } from './telegram';
import type { CachedScan } from './types';

interface Env {
  SIGNALS: KVNamespace;
  GEMINI_API_KEY: string;
  GEMINI_MODEL?: string;
  HANDLES: string;
  ALLOWED_ORIGIN?: string;
  SCAN_TRIGGER_SECRET?: string;
  // Optional Telegram bot push. Both must be set to fire alerts; either
  // missing means "skip the push silently."
  TELEGRAM_BOT_TOKEN?: string;
  TELEGRAM_CHAT_ID?: string;
}

const KV_KEY = 'signals:latest';

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const cors = corsHeaders(env);

    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: cors });
    }

    const url = new URL(request.url);

    if (url.pathname === '/health') {
      return new Response('ok', { headers: { 'Content-Type': 'text/plain', ...cors } });
    }

    if (url.pathname === '/signals/latest' && request.method === 'GET') {
      const cached = await env.SIGNALS.get(KV_KEY);
      if (!cached) {
        return jsonResponse(
          { error: 'no scan yet', hint: 'wait for the daily cron or POST /signals/scan' },
          404,
          cors,
        );
      }
      // Pass through verbatim — already JSON-encoded by the cron writer.
      return new Response(cached, {
        headers: { 'Content-Type': 'application/json', ...cors },
      });
    }

    if (url.pathname === '/signals/scan' && request.method === 'POST') {
      // Force a fresh scan. Guarded by a shared secret so a random
      // visitor can't burn through Gemini quota by curl-spamming us.
      const key = url.searchParams.get('key');
      if (!env.SCAN_TRIGGER_SECRET || key !== env.SCAN_TRIGGER_SECRET) {
        return jsonResponse({ error: 'unauthorized' }, 401, cors);
      }

      // Fire and wait so the caller can see the result inline. Also
      // exercise the Telegram path so manual triggers double as a setup
      // smoke test ("does my bot push actually arrive?").
      try {
        const cached = await runScan(env);
        ctx.waitUntil(maybeNotify(env, cached));
        return jsonResponse(cached, 200, cors);
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        return jsonResponse({ error: msg }, 500, cors);
      }
    }

    return jsonResponse({ error: 'not found' }, 404, cors);
  },

  async scheduled(_event: ScheduledEvent, env: Env, ctx: ExecutionContext): Promise<void> {
    // ctx.waitUntil keeps the worker alive past the handler return so
    // the async scan + KV write + Telegram push can complete.
    ctx.waitUntil(
      runScan(env)
        .then(async (c) => {
          console.log(
            `Cron scan complete. ${c.recommendations.length} recs, ` +
              `${c.rawSignals.length} raw signals, model=${c.model}`,
          );
          await maybeNotify(env, c);
        })
        .catch((err) => console.error('Cron scan failed:', err)),
    );
  },
};

async function runScan(env: Env): Promise<CachedScan> {
  if (!env.GEMINI_API_KEY) {
    throw new Error('GEMINI_API_KEY secret is not set on the worker');
  }

  const handles = parseHandles(env.HANDLES);
  if (handles.length === 0) {
    throw new Error('No handles configured. Set HANDLES var in wrangler.toml.');
  }

  const model = env.GEMINI_MODEL || DEFAULT_MODEL;
  const result = await scanSignals(handles, {
    apiKey: env.GEMINI_API_KEY,
    model,
  });

  const cached: CachedScan = {
    ...result,
    scannedAtUtc: new Date().toISOString(),
    handleCount: handles.length,
    model,
  };

  await env.SIGNALS.put(KV_KEY, JSON.stringify(cached));
  return cached;
}

/**
 * Fires a Telegram message when both the bot token and chat id are
 * configured AND the scan produced a Long/Short rec. Wrapped in a
 * try/catch inside the helper, so a Telegram failure never blocks the
 * KV write or the cron exit code.
 */
async function maybeNotify(env: Env, cached: CachedScan): Promise<void> {
  if (!env.TELEGRAM_BOT_TOKEN || !env.TELEGRAM_CHAT_ID) return;
  if (!shouldNotify(cached)) {
    console.log('Skipping Telegram push: no Long/Short recs.');
    return;
  }
  await sendTelegramAlert(cached, {
    botToken: env.TELEGRAM_BOT_TOKEN,
    chatId: env.TELEGRAM_CHAT_ID,
  });
  console.log('Telegram push sent.');
}

function parseHandles(raw: string | undefined): string[] {
  if (!raw) return [];
  return Array.from(
    new Set(
      raw
        .split(/[,\s]+/)
        .map((h) => h.trim().replace(/^@/, ''))
        .filter((h) => h.length > 0),
    ),
  );
}

function corsHeaders(env: Env): Record<string, string> {
  const origin = env.ALLOWED_ORIGIN || '*';
  return {
    'Access-Control-Allow-Origin': origin,
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Max-Age': '86400',
  };
}

function jsonResponse(
  body: unknown,
  status: number,
  cors: Record<string, string>,
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...cors },
  });
}
