using LifeTracker.Core.Interfaces;
using LifeTracker.Data.Context;
using LifeTracker.Data.Repositories;
using LifeTracker.Web;
using LifeTracker.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// --- Data layer ---
builder.Services.AddSingleton<BrowserDbPersistence>();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={BrowserDbPersistence.DbFilePath}"));

// Scoped: one DbContext per user interaction chain. We expose AppDbContext
// from the factory so repositories dispose it properly.
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddScoped<TradeRepository>();
builder.Services.AddScoped<ITradeRepository>(sp =>
    new PersistingTradeRepository(
        sp.GetRequiredService<TradeRepository>(),
        sp.GetRequiredService<BrowserDbPersistence>()));

// --- AI layer ---
// Settings live in localStorage; the Gemini service reads the API key
// from there on every call so the user can paste/clear it at runtime
// without touching DI.
builder.Services.AddScoped<ISettingsService, BrowserSettings>();
builder.Services.AddScoped<IAiService, GeminiAiService>();

// --- Autocomplete ---
// StaticTickerCatalog pulls wwwroot/data/tickers.json on first search and
// keeps it cached for the session. Also merges tickers the Signals scan
// discovered. We register it by concrete type so the Signals page can
// call AddDiscoveredAsync, and alias ITickerCatalog to the same instance.
builder.Services.AddScoped<StaticTickerCatalog>();
builder.Services.AddScoped<ITickerCatalog>(sp => sp.GetRequiredService<StaticTickerCatalog>());

var host = builder.Build();

// Hydrate SQLite file from IndexedDB (if any) and ensure schema is created.
var persistence = host.Services.GetRequiredService<BrowserDbPersistence>();
await persistence.HydrateAsync();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Persist the (possibly newly created) DB file so first run survives reload.
await persistence.FlushAsync();

await host.RunAsync();
