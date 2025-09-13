namespace BinanceUsdtTicker;

/// <summary>
/// Holds precision information for a trading symbol derived from Binance exchange data.
/// </summary>
public sealed record SymbolPrecision(
    decimal TickSize,
    decimal StepSize,
    decimal MinNotional);
