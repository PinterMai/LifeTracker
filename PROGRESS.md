# Progress log

## 2026-04-20 — Session 3: Blazor PWA pivot

### Decision

Dropped "publishable desktop/mobile app" goal. New target: personal PWA installed to iPhone home screen. No App Store, no paid developer program, no distribution friction.

### What changed

- **Stack pivot**: WPF + (future MAUI) → Blazor WebAssembly + PWA.
- **Data**: EF Core SQLite kept. SQLite compiled to WASM; the `.db` file is persisted to IndexedDB via a small JS interop layer.
- **WPF project kept** in the solution as reference; will be removed once the PWA proves stable.

### What got built

- `LifeTracker.Web` Blazor WASM project scaffolded with PWA template.
- `wasm-tools` .NET workload installed (required for linking SQLite native into WASM).
- `BrowserDbPersistence` service + `wwwroot/js/dbPersistence.js` — bidirectional sync between WASM FS and IndexedDB.
- `PersistingTradeRepository` decorator — flushes to IndexedDB after every mutating call. Data project stays browser-agnostic.
- `Program.cs` wires: `DbContextFactory<AppDbContext>`, scoped `AppDbContext`, `TradeRepository`, decorator `ITradeRepository`. Hydrates DB from IndexedDB and calls `EnsureCreatedAsync` on startup.
- Pages: `Home.razor` (module grid), `Trades/TradeList.razor` (list + stats), `Trades/TradeEdit.razor` (add/edit/delete).
- `NavMenu` trimmed to Home + Trades.
- `manifest.webmanifest` updated with real name, description, theme colors.
- xUnit test project with 12 tests covering `Trade.ProfitLossPercent` (long/short, winners/losers, break-even, divide-by-zero guard, decimals).
- `.github/workflows/deploy.yml` — GitHub Actions builds and deploys to GitHub Pages on every push to `main`.

### Verified

- `dotnet build` succeeds on the whole solution (0 errors, 0 warnings after WASM0001 suppression).
- `dotnet test` — 12/12 passing.

### Not yet done

- First deploy to GitHub Pages — need to enable Pages in repo settings (Settings → Pages → Source: GitHub Actions).
- Smoke test on actual iPhone (install PWA, add a trade, reload, verify persistence).
- `LifeTracker.WPF` removal (wait until PWA is confirmed working on device).

## 2026-04-18 — Session 2: Data layer + WPF scaffolding (later superseded)

- xUnit test project added.
- `Microsoft.Extensions.DependencyInjection` + `Hosting` packages added to WPF.
- `RelayCommand` and `AsyncRelayCommand` written.
- DI setup started in `App.xaml.cs` (not completed — pivoted to Blazor before finish).

## 2026-04-18 — Session 1: Foundation

- Git repo initialized; `.gitignore` for .NET + IDE; GitHub remote `PinterMai/LifeTracker` created and pushed.
- Solution `LifeTracker.slnx` with three projects: `Core`, `Data`, `WPF`. References wired: Core ← Data, Core + Data ← WPF.
- Folder structure per MVVM: `Core/{Models,Interfaces,Services}`, `Data/{Repositories,Context,Migrations}`, `WPF/{Views,ViewModels,Commands}`.
- `Trade` model with `Direction` enum, computed `ProfitLossPercent`.
- EF Core + SQLite installed; `AppDbContext` and `AppDbContextFactory` (design-time).
- `InitialCreate` migration applied; `lifetracker.db` created.
- `ITradeRepository` (Core) + `TradeRepository` (Data) — async CRUD.
- `ViewModelBase` with `INotifyPropertyChanged` + `[CallerMemberName]`.
- `MainWindow` moved into `Views/` for proper MVVM layout.
