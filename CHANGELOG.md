# Changelog

All notable user-facing changes go here. Dates are ISO 8601.

## Unreleased

### Added

- Blazor WebAssembly PWA — install to home screen on iPhone/Android/desktop.
- Trade Journal module: list view with win-rate summary, add/edit/delete.
- Offline data: SQLite database persisted to IndexedDB, survives tab close.
- Automatic deployment to GitHub Pages on every push to `main`.
- **AI trade analysis** via Google Gemini (free tier). Paste key in Settings,
  click "Analyze" on a saved trade to get feedback in context of your history.
- Settings page for managing the AI API key and model.
- **Ticker autocomplete** — wwwroot/data/tickers.json + dynamic discovered
  tickers from the Signals scan, merged into a single catalog.
- **Daily Signals scan** — Gemini reads the last week of posts from your
  trusted X handles and returns aggregated recommendations (Long/Short/
  Watch/Avoid) on top of raw mentions.
- **Signals backend (Cloudflare Worker)** — optional. When configured in
  Settings, a worker runs the daily scan with its own Gemini key and the
  browser just reads the cached snapshot. Keeps your local quota free.

### Changed

- Pivoted from WPF desktop app to Blazor WASM PWA.
- Default Gemini model is now `gemini-flash-latest` instead of the pinned
  `gemini-1.5-flash`. Google retires concrete flash versions and freshly
  issued API keys stop working against them.
- Removed the legacy `LifeTracker.WPF` project from the solution — dead
  code left over from before the PWA pivot.

### Fixed

- Navigation inside the deployed `/LifeTracker/` subpath no longer 404s —
  links are now relative so they resolve against `<base href>`.
- AI feedback card is now readable — previously the text rendered
  near-invisible dark-on-dark because no explicit text color was set.
- Direct-from-browser Signals scans no longer burn the daily Gemini quota
  on every page load — the last result is cached locally and the Scan
  button is gated by a 2-hour cooldown.
