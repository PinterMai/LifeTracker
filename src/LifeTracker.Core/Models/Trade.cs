namespace LifeTracker.Core.Models;

public enum Direction
{
    Long,
    Short
}

public class Trade
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal AmountInvested { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public Direction Direction { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.Now;

    public decimal ProfitLossPercent
    {
        get
        {
            if (OpenPrice == 0) return 0;

            if (Direction == Direction.Long)
                return ((ClosePrice - OpenPrice) / OpenPrice) * 100;
            else
                return ((OpenPrice - ClosePrice) / OpenPrice) * 100;
        }
    }
}
