using LifeTracker.Core.Models;

namespace LifeTracker.Core.Interfaces;

public interface ITradeRepository
{
    Task<IEnumerable<Trade>> GetAllAsync();
    Task<Trade?> GetByIdAsync(int id);
    Task AddAsync(Trade trade);
    /// <summary>
    /// Inserts many trades in one round-trip. Used by CSV import so a
    /// 100-row paste doesn't trigger 100 IndexedDB flushes through the
    /// PersistingTradeRepository decorator.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Trade> trades);
    Task UpdateAsync(Trade trade);
    Task DeleteAsync(int id);
}
