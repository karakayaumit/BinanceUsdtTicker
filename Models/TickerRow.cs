using System;

namespace BinanceUsdtTicker;

public class TickerRow
{
    public string Symbol { get; set; } = string.Empty;
    public double Price { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Volume { get; set; }
    public double ChangePercent { get; set; }
    public DateTime LastUpdate { get; set; }
    public string LastUpdateLocal => LastUpdate.ToLocalTime().ToString("HH:mm:ss");
}
