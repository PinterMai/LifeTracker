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

### Changed

- Pivoted from WPF desktop app to Blazor WASM PWA.

### Fixed

- Navigation inside the deployed `/LifeTracker/` subpath no longer 404s —
  links are now relative so they resolve against `<base href>`.
