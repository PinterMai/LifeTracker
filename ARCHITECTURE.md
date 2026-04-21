# Architecture

## Stack

- **.NET 10** (C#)
- **Blazor WebAssembly + PWA** — deployed as installable web app, runs offline
- **EF Core 10 + SQLite** — SQLite is compiled to WASM and persisted to IndexedDB
- **GitHub Pages** — free static hosting
- **xUnit** — unit tests

## Why Blazor WASM (and not MAUI / native)

Personal deployment target: author's iPhone. MAUI iOS requires Apple Developer Program ($99/yr) for persistent sideloading. Blazor WASM as a PWA installs to the iPhone home screen with no developer account, no store review, no re-signing, and works on any other device the author wants to use it on. C# codebase stays identical to the original WPF design — only the UI layer changed.

## Project layout

```
src/
  LifeTracker.Core/      Models, Interfaces, Services. No I/O, no framework deps.
  LifeTracker.Data/      AppDbContext, migrations, TradeRepository (EF Core).
  LifeTracker.Web/       Blazor WASM UI + browser-specific persistence glue.
  LifeTracker.WPF/       Original WPF skeleton. Kept for history until PWA stabilizes.
tests/
  LifeTracker.Tests/     xUnit tests (business logic only — Data/UI excluded).
```

### Dependency direction

```
Core ← Data ← Web
            ↑
Core ← Data ← Tests
Core ← WPF (legacy)
```

Core has zero outbound dependencies. Everything else points inward. Swapping Web for MAUI or another UI tomorrow does not touch Core/Data.

## Data persistence in the browser

EF Core's SQLite provider requires a filesystem. Browsers have no real one, so the SQLite native library is compiled to WASM and writes to an in-memory filesystem that vanishes on tab close.

Bridge: `BrowserDbPersistence` service.

1. **Startup (`Program.cs`)**: invoke JS `loadDb()` which reads a blob from IndexedDB under key `lifetracker`. If present, the bytes are written to `/lifetracker.db` in the WASM FS. EF Core opens it normally.
2. **After mutation (`PersistingTradeRepository`)**: every `AddAsync`/`UpdateAsync`/`DeleteAsync` calls `FlushAsync`, which reads `/lifetracker.db` from the WASM FS and writes the bytes back to IndexedDB via JS `saveDb()`.
3. **Fresh install**: `EnsureCreatedAsync` creates the schema, then `FlushAsync` persists the empty DB so reload works from first run.

`PersistingTradeRepository` is a decorator over `TradeRepository`. The Data project stays browser-agnostic — the persistence detail lives in the Web project.

## Trade-offs accepted

- **IndexedDB write per mutation.** For a personal trade log (handful of writes per day) this is fine. If writes grow into hundreds per second the flush should be debounced.
- **Whole-file persistence.** The entire SQLite blob is rewritten on each save. Cheap at <1 MB; reconsider if the DB grows past tens of MB.
- **One WASM SQLite worker, single-threaded.** Fine for single-user PWA.
- **No encryption at rest.** IndexedDB is origin-scoped; acceptable for personal device use. API keys (Phase 2) will still be encrypted separately.

## Deployment

GitHub Actions (`.github/workflows/deploy.yml`):

1. Install `wasm-tools` workload (needed to link SQLite native into WASM output).
2. `dotnet publish -c Release`.
3. Rewrite `<base href="/">` to `<base href="/LifeTracker/">` — GitHub Pages project sites serve under a subpath.
4. Add `.nojekyll` — Pages otherwise ignores `_framework/` because it starts with an underscore.
5. Copy `index.html` to `404.html` so Blazor client-side routes work on hard refresh.
6. Upload and deploy via official Pages actions.

Public URL: `https://pintermai.github.io/LifeTracker/` once Pages is enabled in the repo settings (Settings → Pages → Source: GitHub Actions).

## AI layer

Gemini and Claude both accept direct browser calls. We talk to the provider's HTTPS endpoint straight from WASM — no proxy, no server.

- `Core/Interfaces/IAiService` — `AnalyzeTradeAsync`, `TestConnectionAsync`. Pure contract.
- `Core/Services/TradePromptBuilder` — pure prompt assembly (trade + last N history). Unit tested.
- `Core/Interfaces/ISettingsService` + `SettingsKeys` — provider-agnostic key/value abstraction.
- `Web/Services/GeminiAiService` — implementation against `generativelanguage.googleapis.com`. Reads the API key and model from `ISettingsService` on every call so Settings edits take effect immediately.
- `Web/Services/BrowserSettings` — `ISettingsService` backed by browser `localStorage` via a small ES module (`wwwroot/js/settings.js`). Values are origin-scoped, not synced, not encrypted. Same trust boundary as any password the user types into the browser.

Swapping providers is a DI one-liner: write `ClaudeAiService : IAiService`, register it in `Program.cs` instead of `GeminiAiService`. Prompt builder and UI stay identical.

### Why Gemini first, not Claude

Gemini's free tier is real: 15 req/min and 1500 req/day on the `gemini-flash-latest` alias with no credit card. For a personal trade journal that's essentially unlimited. Claude API has no free tier — once the user wants the extra quality we wire in `ClaudeAiService` alongside and let them pick in Settings.

We intentionally default to `gemini-flash-latest` instead of pinning to `gemini-1.5-flash` or similar. Google retires concrete flash versions roughly every 6-9 months and newly issued API keys stop working against them (404 / `UNAVAILABLE` errors). The alias keeps the app working across those rotations without a code change.

## Future modules (designed for, not built)

Same pattern per module: model in Core, DbSet on `AppDbContext`, repository in Data, pages in `Web/Pages/<Module>/`. Phase 3 adds signal ingestion — in a web context this requires a local companion process (browser extension cannot post into its own origin), design TBD.
