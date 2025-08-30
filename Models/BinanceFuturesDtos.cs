namespace BinanceUsdtTicker;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Runtime.Serialization;

public sealed class ExchangeInfo
{
    public List<SymbolInfo> Symbols { get; set; } = new();
}

public sealed class SymbolInfo
{
    public string Symbol { get; set; } = string.Empty;
    [JsonIgnore]
    public List<IFilter> Filters { get; set; } = new();

    [JsonPropertyName("filters")]
    public JsonElement RawFilters { get; set; }

    [JsonConstructor]
    public SymbolInfo() { }

    [OnDeserialized]
    public void OnDeserializedMethod(StreamingContext context)
    {
        if (RawFilters.ValueKind != JsonValueKind.Array) return;
        foreach (var f in RawFilters.EnumerateArray())
        {
            var type = f.GetProperty("filterType").GetString();
            switch (type)
            {
                case "PRICE_FILTER":
                    Filters.Add(new PriceFilter
                    {
                        TickSize = f.TryGetDecimal("tickSize")
                    });
                    break;
                case "LOT_SIZE":
                    Filters.Add(new LotSizeFilter
                    {
                        MinQty = f.TryGetDecimal("minQty"),
                        StepSize = f.TryGetDecimal("stepSize")
                    });
                    break;
                case "MARKET_LOT_SIZE":
                    Filters.Add(new MarketLotSizeFilter
                    {
                        MinQty = f.TryGetDecimal("minQty"),
                        StepSize = f.TryGetDecimal("stepSize")
                    });
                    break;
                case "MIN_NOTIONAL":
                    Filters.Add(new MinNotionalFilter
                    {
                        Notional = f.TryGetDecimal("notional")
                    });
                    break;
            }
        }
    }
}

public interface IFilter { }
public sealed class PriceFilter : IFilter
{
    public decimal TickSize { get; set; }
}
public sealed class LotSizeFilter : IFilter
{
    public decimal MinQty { get; set; }
    public decimal StepSize { get; set; }
}
public sealed class MarketLotSizeFilter : IFilter
{
    public decimal MinQty { get; set; }
    public decimal StepSize { get; set; }
}
public sealed class MinNotionalFilter : IFilter
{
    public decimal Notional { get; set; }
}

public sealed class AccountV3
{
    [JsonPropertyName("availableBalance")] public decimal AvailableBalance { get; set; }
}

public sealed class PositionRisk
{
    public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("positionSide")] public string PositionSideRaw { get; set; } = string.Empty;
    [JsonPropertyName("positionAmt")] public decimal PositionAmt { get; set; }
    [JsonPropertyName("entryPrice")] public decimal EntryPrice { get; set; }
    [JsonPropertyName("markPrice")] public decimal MarkPrice { get; set; }
    [JsonPropertyName("unRealizedProfit")] public decimal UnrealizedPnl { get; set; }
    [JsonPropertyName("notional")] public decimal Notional { get; set; }
    [JsonPropertyName("isolatedWallet")] public decimal IsolatedWallet { get; set; }
    [JsonPropertyName("marginType")] public string MarginType { get; set; } = string.Empty;
    [JsonPropertyName("leverage")] public int Leverage { get; set; }
    [JsonPropertyName("liquidationPrice")] public decimal LiquidationPrice { get; set; }
}

public class LeverageBracket
{
    public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("brackets")] public List<BracketRow> Brackets { get; set; } = new();
}

public sealed class LeverageBracketSingle : LeverageBracket { }

public sealed class BracketRow
{
    [JsonPropertyName("initialLeverage")] public int InitialLeverage { get; set; }
    [JsonPropertyName("notionalCap")] public decimal? NotionalCap { get; set; }
    [JsonPropertyName("maxNotionalValue")] public decimal? MaxNotionalValue { get; set; }
    [JsonPropertyName("maintMarginRatio")] public decimal MaintMarginRatio { get; set; }
}

internal static class JsonExt
{
    public static decimal GetDecimalString(this JsonElement el)
    {
        var s = el.GetString();
        return decimal.Parse(s!, CultureInfo.InvariantCulture);
    }

    public static decimal TryGetDecimal(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p)) return 0m;
        var s = p.GetString();
        return string.IsNullOrWhiteSpace(s) ? 0m : decimal.Parse(s, CultureInfo.InvariantCulture);
    }
}
