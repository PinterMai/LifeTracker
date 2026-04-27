# lifetracker-signals worker

Cloudflare Worker that runs the daily Gemini grounded scan of the user's
trusted X/Twitter handles **server-side**, parks the result in KV, and
exposes it to the LifeTracker PWA via a single GET endpoint.

The browser stops calling Gemini directly for signals — instead it reads
the worker's pre-computed snapshot. That means:

- The Gemini quota is the worker's quota, not the user's per-browser
  quota. Reload-spam costs nothing.
- The scan runs once per day (cron), not on every page open.
- You can change the schedule in `wrangler.toml` without touching the app.

## Endpoints

| Method | Path                                  | Auth                  | Purpose                                |
| ------ | ------------------------------------- | --------------------- | -------------------------------------- |
| GET    | `/health`                             | none                  | liveness check                         |
| GET    | `/signals/latest`                     | none (CORS)           | latest cached scan as JSON             |
| POST   | `/signals/scan?key=<SECRET>`          | `SCAN_TRIGGER_SECRET` | force a fresh scan (returns it inline) |

`/signals/latest` returns 404 until the first scan runs. The shape is
camelCase JSON matching the C# `SignalsScanResult` record (plus
`scannedAtUtc`, `handleCount`, `model` metadata).

## First-time deploy

You need a Cloudflare account (free) and Node.js + npm installed.

```bash
cd workers/signals

# 1. install wrangler locally
npm install

# 2. log into Cloudflare (opens browser)
npx wrangler login

# 3. create the KV namespace; copy the printed id into wrangler.toml
npx wrangler kv namespace create SIGNALS
# -> paste the id under [[kv_namespaces]] in wrangler.toml

# 4. set the Gemini API key as a secret
npx wrangler secret put GEMINI_API_KEY
# (paste your AI Studio key when prompted)

# 5. (optional) set a shared secret for the manual scan trigger
npx wrangler secret put SCAN_TRIGGER_SECRET

# 6. (optional) edit HANDLES in wrangler.toml [vars] if your trusted
#    list changed since this README was written

# 7. deploy
npx wrangler deploy
```

After deploy you'll get a URL like
`https://lifetracker-signals.<your-subdomain>.workers.dev`. Paste that
into the LifeTracker app at **Settings → Signals backend URL**.

## Force the first scan

The cron runs at 07:30 UTC daily. To populate KV immediately:

```bash
curl -X POST "https://<your-url>/signals/scan?key=<SCAN_TRIGGER_SECRET>"
```

Or use the Cloudflare dashboard: **Workers & Pages → your worker → Triggers
→ "Trigger scheduled handler"**.

## Live logs

```bash
npx wrangler tail
```

Each cron run logs `Cron scan complete. <N> recs, <M> raw signals` on
success or the Gemini error body on failure.

## Updating handles

Edit the `HANDLES` value in `wrangler.toml` (comma-separated, no `@`),
then `npx wrangler deploy`. Settings UI in the app no longer feeds the
worker — the worker is the source of truth for the cron's input.
