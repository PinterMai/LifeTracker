using Microsoft.JSInterop;

namespace LifeTracker.Web.Services;

/// <summary>
/// Bridges the SQLite file in the WASM virtual filesystem with IndexedDB.
/// On startup: hydrate WASM FS from IndexedDB blob.
/// After SaveChanges: dump WASM FS file back to IndexedDB.
/// </summary>
public sealed class BrowserDbPersistence : IAsyncDisposable
{
    public const string DbFilePath = "/lifetracker.db";

    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public BrowserDbPersistence(IJSRuntime js)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(() =>
            js.InvokeAsync<IJSObjectReference>("import", "./js/dbPersistence.js").AsTask());
    }

    /// <summary>
    /// Loads the persisted .db file from IndexedDB into the WASM FS.
    /// No-op if nothing is persisted yet — EF Core will create a fresh DB.
    /// </summary>
    public async Task HydrateAsync()
    {
        var module = await _moduleTask.Value;
        var bytes = await module.InvokeAsync<byte[]?>("loadDb");
        if (bytes is { Length: > 0 })
        {
            await File.WriteAllBytesAsync(DbFilePath, bytes);
        }
    }

    /// <summary>
    /// Dumps the current .db file from WASM FS to IndexedDB. Call after every SaveChanges.
    /// </summary>
    public async Task FlushAsync()
    {
        if (!File.Exists(DbFilePath))
            return;

        var bytes = await File.ReadAllBytesAsync(DbFilePath);
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("saveDb", bytes);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
