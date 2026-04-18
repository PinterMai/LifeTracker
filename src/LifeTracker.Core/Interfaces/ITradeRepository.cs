using LifeTracker.Core.Models;

namespace LifeTracker.Core.Interfaces;

public interface ITradeRepository
{
    Task<IEnumerable<Trade>> GetAllAsync();
    Task<Trade?> GetByIdAsync(int id);
    Task AddAsync(Trade trade);
    Task UpdateAsync(Trade trade);
    Task DeleteAsync(int id);
}
