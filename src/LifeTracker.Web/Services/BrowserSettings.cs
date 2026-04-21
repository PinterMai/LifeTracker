using LifeTracker.Core.Interfaces;
using Microsoft.JSInterop;

namespace LifeTracker.Web.Services;

/// <summary>
/// ISettingsService backed by the browser's localStorage via a small
/// ES module. Values are origin-scoped and persist across reloads.
/// Not encrypted — the user's own API key stays in their own browser,
/// which is the same trust boundary as any password they'd type in.
/// </summary>
public sealed class BrowserSettings : ISettingsService, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public BrowserSettings(IJSRuntime js)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(() =>
            js.InvokeAsync<IJSObjectReference>("import", "./js/settings.js").AsTask());
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value.ConfigureAwait(false);
        return await module.InvokeAsync<string?>("get", cancellationToken, key).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value.ConfigureAwait(false);
        await module.InvokeVoidAsync("set", cancellationToken, key, value).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value.ConfigureAwait(false);
        await module.InvokeVoidAsync("remove", cancellationToken, key).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value.ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
    }
}
