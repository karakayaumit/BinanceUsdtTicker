using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Serialization;
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
        private readonly BinanceFuturesRestClient _client;
        private readonly SymbolMetaCache _cache = new();

        public OrderPreviewService(BinanceFuturesRestClient client)
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
                var pr = await _client.GetPositionRiskAsync(symbol, ct);
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

    #region REST Client + Cache + DTOs

    public class BinanceFuturesRestClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _baseUrl;

        public BinanceFuturesRestClient(HttpClient http, string apiKey, string apiSecret, bool useTestnet = false)
        {
            _http = http;
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _baseUrl = useTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";
        }

        public async Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct)
        {
            var s = await SendAsync($"/fapi/v1/ticker/price?symbol={symbol}", HttpMethod.Get, false, ct);
            using var d = JsonDocument.Parse(s);
            return d.RootElement.GetProperty("price").GetDecimalString();
        }

        public async Task<decimal> GetMarkPriceAsync(string symbol, CancellationToken ct)
        {
            var s = await SendAsync($"/fapi/v1/premiumIndex?symbol={symbol}", HttpMethod.Get, false, ct);
            using var d = JsonDocument.Parse(s);
            return d.RootElement.GetProperty("markPrice").GetDecimalString();
        }

        public async Task<AccountV3> GetAccountV3Async(CancellationToken ct)
        {
            var s = await SendSignedAsync("/fapi/v3/account", HttpMethod.Get, null, ct);
            return JsonSerializer.Deserialize<AccountV3>(s, JsonOptions) ?? new();
        }

        public async Task<List<PositionRisk>> GetPositionRiskAsync(string symbol, CancellationToken ct)
        {
            var qp = new Dictionary<string, string> { ["symbol"] = symbol };
            var s = await SendSignedAsync("/fapi/v3/positionRisk", HttpMethod.Get, qp, ct);
            return JsonSerializer.Deserialize<List<PositionRisk>>(s, JsonOptions) ?? new();
        }

        public async Task<decimal> GetAbsNotionalAsync(string symbol, CancellationToken ct)
        {
            var list = await GetPositionRiskAsync(symbol, ct);
            return list.Sum(x => Math.Abs(x.Notional));
        }

        public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct)
        {
            var s = await SendAsync("/fapi/v1/exchangeInfo", HttpMethod.Get, false, ct);
            return JsonSerializer.Deserialize<ExchangeInfo>(s, JsonOptions) ?? new();
        }

        public async Task<List<LeverageBracket>> GetLeverageBracketsAsync(string symbol, CancellationToken ct)
        {
            var s = await SendSignedAsync("/fapi/v1/leverageBracket", HttpMethod.Get, new() { ["symbol"] = symbol }, ct);
            if (s.TrimStart().StartsWith("["))
                return JsonSerializer.Deserialize<List<LeverageBracket>>(s, JsonOptions) ?? new();
            var one = JsonSerializer.Deserialize<LeverageBracketSingle>(s, JsonOptions);
            return one != null ? new List<LeverageBracket> { one } : new();
        }

        private async Task<string> SendSignedAsync(string path, HttpMethod method, Dictionary<string, string>? query, CancellationToken ct)
        {
            query ??= new();
            query["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            var sig = Sign(qs, _apiSecret);
            var url = $"{_baseUrl}{path}?{qs}&signature={sig}";
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Add("X-MBX-APIKEY", _apiKey);
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync(ct);
        }

        private async Task<string> SendAsync(string path, HttpMethod method, bool signed, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(method, _baseUrl + path);
            if (signed) req.Headers.Add("X-MBX-APIKEY", _apiKey);
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync(ct);
        }

        private static string Sign(string data, string secret)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        }

        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    internal sealed class SymbolMetaCache
    {
        private readonly Dictionary<string, SymbolMeta> _map = new(StringComparer.OrdinalIgnoreCase);
        private ExchangeInfo? _ex;

        public async Task<SymbolMeta> GetOrLoadAsync(BinanceFuturesRestClient client, string symbol, CancellationToken ct)
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

    public sealed class ExchangeInfo
    {
        public List<SymbolInfo> Symbols { get; set; } = new();
    }

    public sealed class SymbolInfo
    {
        public string Symbol { get; set; } = "";
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
        public string Symbol { get; set; } = "";
        [JsonPropertyName("positionSide")] public string PositionSideRaw { get; set; } = "";
        [JsonPropertyName("positionAmt")] public decimal PositionAmt { get; set; }
        [JsonPropertyName("entryPrice")] public decimal EntryPrice { get; set; }
        [JsonPropertyName("markPrice")] public decimal MarkPrice { get; set; }
        [JsonPropertyName("unRealizedProfit")] public decimal UnrealizedPnl { get; set; }
        [JsonPropertyName("notional")] public decimal Notional { get; set; }
        [JsonPropertyName("isolatedWallet")] public decimal IsolatedWallet { get; set; }
    }

    public sealed class LeverageBracketSingle : LeverageBracket
    {
    }

    public class LeverageBracket
    {
        public string Symbol { get; set; } = "";
        [JsonPropertyName("brackets")] public List<BracketRow> Brackets { get; set; } = new();
    }

    public sealed class BracketRow
    {
        [JsonPropertyName("initialLeverage")] public int InitialLeverage { get; set; }
        [JsonPropertyName("notionalCap")] public decimal? NotionalCap { get; set; }
        [JsonPropertyName("maxNotionalValue")] public decimal? MaxNotionalValue { get; set; }
        [JsonPropertyName("maintMarginRatio")] public decimal MaintMarginRatio { get; set; }
    }

    #endregion

    #region Json helpers
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
    #endregion
}
