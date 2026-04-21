# Life Tracker

Personal habit tracker as a Blazor WebAssembly PWA. Starts with a Trade Journal module; additional trackers (food, workout, sleep, journaling) planned.

**Live:** https://pintermai.github.io/LifeTracker/ *(after first Pages deploy)*

## Install on iPhone

1. Open the live URL in Safari.
2. Tap the Share icon → **Add to Home Screen**.
3. Launch from the home screen — runs like a native app, works offline after first load.

## Stack

- .NET 10, C#
- Blazor WebAssembly + PWA
- EF Core 10 + SQLite (compiled to WASM, persisted to IndexedDB)
- xUnit for tests
- GitHub Actions → GitHub Pages for hosting

See [ARCHITECTURE.md](ARCHITECTURE.md) for design decisions and how browser persistence works.

## Local development

```bash
dotnet restore
dotnet run --project src/LifeTracker.Web
```

Then browse to the URL printed in the console.

### Run tests

```bash
dotnet test
```

### First-time setup

You need the `wasm-tools` workload (needed to link SQLite native into WASM):

```bash
dotnet workload install wasm-tools
```

## Repo layout

```
src/
  LifeTracker.Core/   Models, interfaces. No framework deps.
  LifeTracker.Data/   EF Core DbContext + repositories.
  LifeTracker.Web/    Blazor WASM UI.
  LifeTracker.WPF/    Historical — original desktop skeleton.
tests/
  LifeTracker.Tests/  xUnit.
```

## Status

Phase 1 (Trade Journal MVP) — in progress. See [PROGRESS.md](PROGRESS.md).
