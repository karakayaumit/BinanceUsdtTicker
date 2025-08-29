using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceUsdtTicker.Trading
{
    #region Enums & Request/Response

    public enum OrderType { Market, Limit }
    public enum OrderSide { Buy, Sell }
    public enum MarginMode { Cross, Isolated }
    public enum PositionSide { OneWay, Long, Short }

    public sealed record OrderPreviewRequest(
        string Symbol,
        OrderType OrderType,
        OrderSide Side,
        PositionSide PositionSide,
        MarginMode MarginMode,
        int Leverage,
        decimal WalletPercent,
        decimal? LimitPrice = null,
        decimal MarketSlippageBps = 10m,
        decimal? MakerFeeRate = null,
        decimal? TakerFeeRate = null
    );

    public sealed class OrderPreview
    {
        public string Symbol { get; init; } = "";
        public OrderType OrderType { get; init; }
        public OrderSide Side { get; init; }
        public PositionSide PositionSide { get; init; }
        public MarginMode MarginMode { get; init; }
        public int Leverage { get; init; }

        public decimal LastPrice { get; init; }
        public decimal MarkPrice { get; init; }
        public decimal ReferencePrice { get; init; }
        public decimal InitialMarginRate { get; init; }

        public decimal LotStepSize { get; init; }
        public decimal LotMinQty { get; init; }
        public decimal? MarketLotStepSize { get; init; }
        public decimal? MarketLotMinQty { get; init; }
        public decimal? MinNotional { get; init; }
        public decimal? NotionalCap { get; init; }

        public decimal UsableBalance { get; init; }
        public decimal ExistingAbsNotional { get; init; }

        public decimal OpenLossPerUnit { get; init; }
        public decimal MaxQtyByBalance { get; init; }
        public decimal MaxQtyByBracket { get; init; }
        public decimal MaxQtyByFilters { get; init; }
        public decimal MaxQtyFinal { get; init; }

        public decimal SuggestedQty => MaxQtyFinal;
        public decimal SuggestedNotional => Round4(SuggestedQty * ReferencePrice);
        public decimal InitialMargin => Round4(SuggestedNotional * InitialMarginRate);
        public decimal OpenLoss => Round4(SuggestedQty * OpenLossPerUnit);
        public decimal Cost => Round4(InitialMargin + OpenLoss);
        public decimal? EstFee => null;

        public List<string> Warnings { get; init; } = new();

        static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    }

    #endregion

    #region Service

    public class OrderPreviewService
    {
        private readonly BinanceApiService _client;
        private readonly SymbolMetaCache _cache = new();

        public OrderPreviewService(BinanceApiService client)
        {
            _client = client;
        }

        public async Task<OrderPreview> ComputeAsync(OrderPreviewRequest r, CancellationToken ct = default)
        {
            var symbol = r.Symbol.ToUpperInvariant();
            if (r.OrderType == OrderType.Limit && r.LimitPrice is null)
                throw new ArgumentException("Limit emri için LimitPrice gereklidir.");
            if (r.Leverage < 1) throw new ArgumentException("Leverage >= 1 olmalı");
            if (r.WalletPercent is < 0m or > 1m) throw new ArgumentException("WalletPercent 0..1 aralığında olmalı");

            var last = await _client.GetLastPriceAsync(symbol, ct);
            var mark = await _client.GetMarkPriceAsync(symbol, ct);
            decimal refPx = r.OrderType switch
            {
                OrderType.Market => r.Side == OrderSide.Buy
                    ? last * (1m + r.MarketSlippageBps / 10_000m)
                    : last * (1m - r.MarketSlippageBps / 10_000m),
                _ => r.LimitPrice!.Value
            };

            var meta = await _cache.GetOrLoadAsync(_client, symbol, ct);
            var lot = r.OrderType == OrderType.Market
                ? (meta.MarketLot != null
                    ? new LotSizeFilter { MinQty = meta.MarketLot.MinQty, StepSize = meta.MarketLot.StepSize }
                    : meta.Lot)
                : meta.Lot;
            var minNotional = meta.MinNotional;

            decimal usable;
            decimal existingAbsNotional = 0m;
            if (r.MarginMode == MarginMode.Cross)
            {
                var acc = await _client.GetAccountV3Async(ct);
                usable = acc.AvailableBalance * r.WalletPercent;
                existingAbsNotional = await _client.GetAbsNotionalAsync(symbol, ct);
            }
            else
            {
                var pr = await _client.GetPositionRiskV3Async(symbol, ct);
                var isoWallet = pr.Sum(x => x.IsolatedWallet);
                usable = isoWallet * r.WalletPercent;
                existingAbsNotional = pr.Sum(x => Math.Abs(x.Notional));
            }

            var cap = meta.ResolveNotionalCapForLeverage(r.Leverage);

            var imr = 1m / r.Leverage;
            decimal openLossPerUnit = 0m;
            if (r.Side == OrderSide.Buy)
            {
                if (refPx > mark) openLossPerUnit = refPx - mark;
            }
            else
            {
                if (refPx < mark) openLossPerUnit = mark - refPx;
            }
            var denom = refPx * imr + openLossPerUnit;
            var maxByBalance = denom > 0m ? usable / denom : decimal.Zero;

            decimal maxByBracket = decimal.MaxValue;
            if (cap != null)
            {
                var capFree = cap.Value - existingAbsNotional;
                if (capFree < 0) capFree = 0;
                maxByBracket = refPx > 0 ? capFree / refPx : 0;
            }

            var maxRaw = MinSafe(maxByBalance, maxByBracket);
            var maxByFilters = QuantizeDown(maxRaw, lot.StepSize);
            if (maxByFilters < lot.MinQty) maxByFilters = 0m;

            var warnings = new List<string>();
            if (maxByFilters > 0 && minNotional != null)
            {
                var notion = maxByFilters * refPx;
                if (notion < minNotional)
                {
                    warnings.Add($"Önerilen miktar MIN_NOTIONAL'ı karşılamıyor (gereken ≥ {minNotional}).");
                    maxByFilters = 0m;
                }
            }

            if (cap != null && maxByBracket <= maxByBalance)
                warnings.Add("Risk kademesi (notional cap) limiti nedeniyle MAX kısıtlandı.");

            if (maxByFilters == 0m)
                warnings.Add("Yeterli bakiye/limit/filtre koşulları sağlanamadı.");

            return new OrderPreview
            {
                Symbol = symbol,
                OrderType = r.OrderType,
                Side = r.Side,
                PositionSide = r.PositionSide,
                MarginMode = r.MarginMode,
                Leverage = r.Leverage,

                LastPrice = last,
                MarkPrice = mark,
                ReferencePrice = refPx,
                InitialMarginRate = imr,

                LotStepSize = lot.StepSize,
                LotMinQty = lot.MinQty,
                MarketLotStepSize = meta.MarketLot?.StepSize,
                MarketLotMinQty = meta.MarketLot?.MinQty,
                MinNotional = minNotional,
                NotionalCap = cap,

                UsableBalance = usable,
                ExistingAbsNotional = existingAbsNotional,

                OpenLossPerUnit = openLossPerUnit,
                MaxQtyByBalance = Round6(maxByBalance),
                MaxQtyByBracket = Round6(maxByBracket),
                MaxQtyByFilters = Round6(maxByFilters),
                MaxQtyFinal = Round6(maxByFilters),
                Warnings = warnings
            };

            static decimal MinSafe(decimal a, decimal b) => a < b ? a : b;
            static decimal QuantizeDown(decimal v, decimal step)
            {
                if (step <= 0m) return v;
                var n = Math.Floor(v / step);
                return n * step;
            }
            static decimal Round6(decimal v) => Math.Round(v, 6, MidpointRounding.ToZero);
        }
    }

    #endregion

    #region Cache


    internal sealed class SymbolMetaCache
    {
        private readonly Dictionary<string, SymbolMeta> _map = new(StringComparer.OrdinalIgnoreCase);
        private ExchangeInfo? _ex;

        public async Task<SymbolMeta> GetOrLoadAsync(BinanceApiService client, string symbol, CancellationToken ct)
        {
            if (_map.TryGetValue(symbol, out var ok)) return ok;

            _ex ??= await client.GetExchangeInfoAsync(ct);
            var sym = _ex.Symbols.FirstOrDefault(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"exchangeInfo’da {symbol} bulunamadı.");

            var lot = sym.Filters.OfType<LotSizeFilter>().FirstOrDefault() ?? new LotSizeFilter();
            var mlot = sym.Filters.OfType<MarketLotSizeFilter>().FirstOrDefault();
            var minNotional = sym.Filters.OfType<MinNotionalFilter>().FirstOrDefault()?.Notional;

            var lev = await client.GetLeverageBracketsAsync(symbol, ct);

            var meta = new SymbolMeta(symbol, lot, mlot, minNotional, lev);
            _map[symbol] = meta;
            return meta;
        }
    }

    internal sealed record SymbolMeta(
        string Symbol,
        LotSizeFilter Lot,
        MarketLotSizeFilter? MarketLot,
        decimal? MinNotional,
        List<LeverageBracket> Brackets)
    {
        public decimal? ResolveNotionalCapForLeverage(int leverage)
        {
            if (Brackets == null || Brackets.Count == 0) return null;
            var eligible = Brackets
                .SelectMany(b => b.Brackets.Select(x => new { x.InitialLeverage, Cap = x.NotionalCap ?? x.MaxNotionalValue }))
                .Where(x => x.InitialLeverage >= leverage && x.Cap != null)
                .Select(x => x.Cap!.Value)
                .DefaultIfEmpty((decimal)0);
            var minCap = eligible.Min();
            return minCap == 0 ? null : minCap;
        }
    }

#endregion

}
