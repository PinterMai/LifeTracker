using LifeTracker.Core.Interfaces;
using LifeTracker.Core.Models;
using LifeTracker.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace LifeTracker.Data.Repositories;

public class TradeRepository : ITradeRepository
{
    private readonly AppDbContext _context;

    public TradeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Trade>> GetAllAsync()
    {
        return await _context.Trades.ToListAsync();
    }

    public async Task<Trade?> GetByIdAsync(int id)
    {
        return await _context.Trades.FindAsync(id);
    }

    public async Task AddAsync(Trade trade)
    {
        await _context.Trades.AddAsync(trade);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Trade trade)
    {
        _context.Trades.Update(trade);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var trade = await GetByIdAsync(id);
        if (trade is not null)
        {
            _context.Trades.Remove(trade);
            await _context.SaveChangesAsync();
        }
    }
}
