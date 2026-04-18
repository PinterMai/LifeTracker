using LifeTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeTracker.Data.Context;

public class AppDbContext : DbContext
{
    public DbSet<Trade> Trades { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
