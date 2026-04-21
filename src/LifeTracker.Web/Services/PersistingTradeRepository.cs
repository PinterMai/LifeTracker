using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;

namespace LifeTracker.Web.Services;

/// <summary>
/// Decorates an ITradeRepository so that every mutating call flushes the
/// SQLite file back to IndexedDB. Keeps Data project browser-agnostic.
/// </summary>
public sealed class PersistingTradeRepository : ITradeRepository
{
    private readonly ITradeRepository _inner;
    private readonly BrowserDbPersistence _persistence;

    public PersistingTradeRepository(ITradeRepository inner, BrowserDbPersistence persistence)
    {
        _inner = inner;
        _persistence = persistence;
    }

    public Task<IEnumerable<Trade>> GetAllAsync() => _inner.GetAllAsync();

    public Task<Trade?> GetByIdAsync(int id) => _inner.GetByIdAsync(id);

    public async Task AddAsync(Trade trade)
    {
        await _inner.AddAsync(trade);
        await _persistence.FlushAsync();
    }

    public async Task UpdateAsync(Trade trade)
    {
        await _inner.UpdateAsync(trade);
        await _persistence.FlushAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _inner.DeleteAsync(id);
        await _persistence.FlushAsync();
    }
}
