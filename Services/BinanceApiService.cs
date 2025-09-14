using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Collections.Concurrent;
using BinanceUsdtTicker.Models;
using BinanceUsdtTicker.Helpers;


namespace BinanceUsdtTicker
{
    /// <summary>
    /// Basit Binance Futures REST API istemcisi. API anahtarı ve gizli anahtarla imzalı istek gönderir.
    /// </summary>
    public class BinanceApiService : BinanceRestClientBase
    {
        private readonly Dictionary<string, SymbolPrecision> _symbolFilters = new(StringComparer.OrdinalIgnoreCase);
        private ExchangeInfo? _exchangeInfo;
        private readonly ConcurrentDictionary<string, SymbolRules> _rulesCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan RulesTtl = TimeSpan.FromMinutes(15);

        private record SymbolRules(
            decimal TickSize,
            decimal StepSize,
            decimal? MinQty,
            decimal? MinNotional,
            decimal? MinPrice,
            decimal? MaxPrice,
            DateTime FetchedAt);
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public BinanceApiService() : base(new HttpClient { BaseAddress = new Uri("https://fapi.binance.com") })
        {
        }

        public BinanceApiService(HttpClient httpClient) : base(httpClient)
        {
            if (httpClient.BaseAddress == null)
                httpClient.BaseAddress = new Uri("https://fapi.binance.com");
        }
        public async Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct = default)
        {
            var s = await SendAsync(HttpMethod.Get, $"/fapi/v1/ticker/price?symbol={symbol}", ct);
            using var d = JsonDocument.Parse(s);
            return d.RootElement.GetProperty("price").GetDecimalString();
        }

        public async Task<decimal> GetMarkPriceAsync(string symbol, CancellationToken ct = default)
        {
            var s = await SendAsync(HttpMethod.Get, $"/fapi/v1/premiumIndex?symbol={symbol}", ct);
            using var d = JsonDocument.Parse(s);
            return d.RootElement.GetProperty("markPrice").GetDecimalString();
        }

        public async Task<AccountV3> GetAccountV3Async(CancellationToken ct = default)
        {
            var s = await SendSignedAsync(HttpMethod.Get, "/fapi/v3/account", null, ct);
            return JsonSerializer.Deserialize<AccountV3>(s, JsonOptions) ?? new();
        }

        public async Task<IList<PositionRisk>> GetPositionRiskV3Async(string symbol, CancellationToken ct = default)
        {
            var qp = new Dictionary<string, string> { ["symbol"] = symbol };
            var s = await SendSignedAsync(HttpMethod.Get, "/fapi/v3/positionRisk", qp, ct);
            return JsonSerializer.Deserialize<IList<PositionRisk>>(s, JsonOptions) ?? new List<PositionRisk>();
        }

        public async Task<decimal> GetAbsNotionalAsync(string symbol, CancellationToken ct = default)
        {
            var list = await GetPositionRiskV3Async(symbol, ct);
            return list.Sum(x => Math.Abs(x.Notional));
        }

        public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
        {
            if (_exchangeInfo != null) return _exchangeInfo;
            var s = await SendAsync(HttpMethod.Get, "/fapi/v1/exchangeInfo", ct);
            _exchangeInfo = JsonSerializer.Deserialize<ExchangeInfo>(s, JsonOptions) ?? new ExchangeInfo();
            return _exchangeInfo;
        }

        public async Task<IList<LeverageBracket>> GetLeverageBracketsAsync(string symbol, CancellationToken ct = default)
        {
            var s = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/leverageBracket", new Dictionary<string, string> { ["symbol"] = symbol }, ct);
            if (s.TrimStart().StartsWith("["))
                return JsonSerializer.Deserialize<IList<LeverageBracket>>(s, JsonOptions) ?? new List<LeverageBracket>();
            var one = JsonSerializer.Deserialize<LeverageBracketSingle>(s, JsonOptions);
            return one != null ? new List<LeverageBracket> { one } : new List<LeverageBracket>();
        }

        public async Task<IList<PositionRisk>> GetPositionRiskV2Async(string? symbol = null, CancellationToken ct = default)
        {
            var query = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(symbol))
                query["symbol"] = symbol;
            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v2/positionRisk", query, ct);
            return JsonSerializer.Deserialize<IList<PositionRisk>>(json, JsonOptions) ?? new List<PositionRisk>();
        }

        /// <summary>
        /// Örnek amaçlı hesap bilgilerini döner.
        /// </summary>
        public async Task<string> GetAccountInfoAsync()
        {
            return await SendSignedAsync(HttpMethod.Get, "/fapi/v2/account");
        }

        /// <summary>
        /// Hesaptaki varlık bakiyelerini döner.
        /// </summary>
        public async Task<IList<WalletAsset>> GetAccountBalancesAsync()
        {
            var json = await GetAccountInfoAsync();
            var list = new List<WalletAsset>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var item in assets.EnumerateArray())
                    {
                        var asset = item.GetProperty("asset").GetString() ?? string.Empty;
                        decimal.TryParse(item.GetProperty("walletBalance").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var balance);
                        decimal.TryParse(item.GetProperty("availableBalance").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var available);
                        if (balance > 0m || available > 0m)
                            list.Add(new WalletAsset { Asset = asset, Balance = balance, Available = available });
                    }
                }
                else if (doc.RootElement.TryGetProperty("msg", out var msg))
                {
                    throw new Exception(msg.GetString() ?? "Bilinmeyen hata");
                }
                else
                {
                    throw new Exception("Beklenmeyen yanıt alındı.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cüzdan bilgileri alınırken hata oluştu: {ex.Message}", ex);
            }

            return list;
        }

        /// <summary>
        /// Belirtilen sembol için mevcut pozisyon bilgilerini döner.
        /// </summary>

        public async Task<(string MarginType, int Leverage)> GetPositionInfoAsync(string symbol)
        {
            var list = await GetPositionRiskV2Async(symbol);
            var el = list.FirstOrDefault();
            if (el != null)
                return (el.MarginType.ToLowerInvariant(), el.Leverage);
            return ("cross", 1);
        }
        /// <summary>
        /// Sembol için kullanılabilir kaldıraç değerlerini ve bakım marj oranını döner.
        /// </summary>
        public async Task<(IList<int> Options, decimal MaintMarginRatio)> GetLeverageOptionsAsync(string symbol)
        {
            var brackets = await GetLeverageBracketsAsync(symbol);
            var list = new List<int>();
            decimal mmr = 0m;
            var first = brackets.FirstOrDefault()?.Brackets.FirstOrDefault();
            if (first != null)
            {
                mmr = first.MaintMarginRatio;
                var max = brackets.First().Brackets.Max(b => b.InitialLeverage);
                for (int i = 1; i <= max; i++) list.Add(i);
            }
            return (list, mmr);
        }

        /// <summary>
        /// Belirtilen sembol için gerçekleşen işlemleri döner.
        /// </summary>
        /// <param name="symbol">İşlem yapılacak sembol.</param>
        /// <param name="limit">Döndürülecek maksimum kayıt sayısı.</param>
        /// <param name="startTime">Başlangıç zamanı (UTC). Belirtilirse bu tarihten sonraki işlemler getirilir.</param>
        public async Task<IList<FuturesTrade>> GetUserTradesAsync(string symbol, int limit = 50, DateTime? startTime = null)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
            };

            if (startTime.HasValue)
            {
                var ms = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
                query["startTime"] = ms.ToString(CultureInfo.InvariantCulture);
            }

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/userTrades", query);
            var list = new List<FuturesTrade>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    decimal.TryParse(el.GetProperty("qty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                    decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    decimal.TryParse(el.GetProperty("realizedPnl").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var pnl);
                    decimal.TryParse(el.GetProperty("commission").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var fee);
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesTrade
                    {
                        Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
                        Fee = fee,
                        RealizedPnl = pnl,
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
                    });
                }
            }
            catch { }
            return list;
        }

        public async Task<IList<FuturesOrder>> GetAllOrdersAsync(string symbol, int limit = 50, DateTime? startTime = null)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
            };

            if (startTime.HasValue)
            {
                var ms = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
                query["startTime"] = ms.ToString(CultureInfo.InvariantCulture);
            }

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/allOrders", query);
            var list = new List<FuturesOrder>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    long orderId = el.GetProperty("orderId").GetInt64();
                    decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                    decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    decimal.TryParse(el.GetProperty("executedQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var filled);
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesOrder
                    {
                        OrderId = orderId,
                        Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
                        Filled = filled,
                        Status = el.GetProperty("status").GetString() ?? string.Empty,
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
                    });
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Hesaptaki açık emirleri döner. Sembol verilmezse tüm semboller için çalışır.
        /// </summary>
        public async Task<IList<FuturesOrder>> GetOpenOrdersAsync(string? symbol = null)
        {
            var query = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(symbol))
                query["symbol"] = symbol;

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/openOrders", query);
            var list = new List<FuturesOrder>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    long orderId = el.GetProperty("orderId").GetInt64();
                    decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                    decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    decimal.TryParse(el.GetProperty("executedQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var filled);
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesOrder
                    {
                        OrderId = orderId,
                        Symbol = el.GetProperty("symbol").GetString() ?? string.Empty,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
                        Filled = filled,
                        Status = el.GetProperty("status").GetString() ?? string.Empty,
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
                    });
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Belirtilen sembol için tek bir açık emri döner.
        /// </summary>
        public async Task<FuturesOrder?> GetOpenOrderAsync(string symbol, long? orderId = null, string? origClientOrderId = null)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol
            };
            if (orderId.HasValue)
                query["orderId"] = orderId.Value.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(origClientOrderId))
                query["origClientOrderId"] = origClientOrderId;

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/openOrder", query);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var el = doc.RootElement;
                long parsedOrderId = el.GetProperty("orderId").GetInt64();
                decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                decimal.TryParse(el.GetProperty("executedQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var filled);
                long time = el.GetProperty("time").GetInt64();
                return new FuturesOrder
                {
                    OrderId = parsedOrderId,
                    Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                    Side = el.GetProperty("side").GetString() ?? string.Empty,
                    Quantity = qty,
                    Price = price,
                    Filled = filled,
                    Status = el.GetProperty("status").GetString() ?? string.Empty,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task CancelOrderAsync(string symbol, long orderId)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["orderId"] = orderId.ToString(CultureInfo.InvariantCulture)
            };
            await SendSignedAsync(HttpMethod.Delete, "/fapi/v1/order", query);
        }

        public async Task SetLeverageAsync(string symbol, int leverage)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["leverage"] = leverage.ToString(CultureInfo.InvariantCulture)
            };
            await SendSignedAsync(HttpMethod.Post, "/fapi/v1/leverage", query);
        }

        public async Task SetMarginTypeAsync(string symbol, string marginType)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["marginType"] = marginType.ToUpperInvariant()
            };
            try
            {
                await SendSignedAsync(HttpMethod.Post, "/fapi/v1/marginType", query);
            }
            catch { }
        }

        public async Task<SymbolPrecision> GetSymbolFiltersAsync(string symbol)
        {
            if (_symbolFilters.TryGetValue(symbol, out var f))
                return f;

            var json = await SendAsync(HttpMethod.Get, $"/fapi/v1/exchangeInfo?symbol={symbol}");
            var info = JsonSerializer.Deserialize<ExchangeInfo>(json, JsonOptions) ?? new ExchangeInfo();

            decimal tick = 0m;
            decimal step = 0m;
            decimal minNotional = 0m;

            var sym = info.Symbols.FirstOrDefault();
            if (sym != null)
            {
                var price = sym.Filters.OfType<PriceFilter>().FirstOrDefault();
                var lot = sym.Filters.OfType<LotSizeFilter>().FirstOrDefault();
                var marketLot = sym.Filters.OfType<MarketLotSizeFilter>().FirstOrDefault();
                var minNot = sym.Filters.OfType<MinNotionalFilter>().FirstOrDefault();
                if (price != null) tick = price.TickSize;

                // Some symbols have different step sizes for market and limit orders.
                // Use the more restrictive (larger) step size to avoid sending a quantity
                // with higher precision than allowed by the exchange.
                if (lot != null && marketLot != null)
                    step = Math.Max(lot.StepSize, marketLot.StepSize);
                else if (lot != null)
                    step = lot.StepSize;
                else if (marketLot != null)
                    step = marketLot.StepSize;

                if (minNot != null)
                    minNotional = minNot.Notional;
            }

            if (tick >= 1m)
                throw new InvalidOperationException($"Suspicious tickSize {tick} for {symbol}. Check endpoint/baseUrl.");

            var res = new SymbolPrecision(tick, step, minNotional);
            _symbolFilters[symbol] = res;
            return res;
        }

        public async Task<(SymbolPrecision Filters, decimal? Price, decimal Quantity)> ApplyOrderPrecisionAsync(string symbol, decimal? price, decimal quantity)
        {
            var filters = await GetSymbolFiltersAsync(symbol);
            var qtyAdj = filters.StepSize > 0 ? QuantizeDown(quantity, filters.StepSize) : quantity;
            if (qtyAdj <= 0m)
                throw new ArgumentException("Quantity is too small for the step size.");

            decimal? priceAdj = null;
            if (price.HasValue)
                priceAdj = filters.TickSize > 0 ? QuantizeToTick(price.Value, filters.TickSize) : price.Value;

            return (filters, priceAdj, qtyAdj);
        }

        private async Task<SymbolRules> GetSymbolRulesAsync(string symbol, CancellationToken ct = default)
        {
            if (_rulesCache.TryGetValue(symbol, out var cached) &&
                (DateTime.UtcNow - cached.FetchedAt) < RulesTtl)
                return cached;

            var json = await SendAsync(HttpMethod.Get, $"/fapi/v1/exchangeInfo?symbol={symbol}", ct);

            decimal ParseInv(string s) => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

            decimal tick = 0m, step = 0m;
            decimal? minQty = null, minNotional = null, minPrice = null, maxPrice = null;

            using var doc = JsonDocument.Parse(json);
            var sym = doc.RootElement.GetProperty("symbols").EnumerateArray().FirstOrDefault();
            if (sym.ValueKind != JsonValueKind.Undefined)
            {
                var filtersJson = sym.GetProperty("filters");
                foreach (var f in filtersJson.EnumerateArray())
                {
                    var type = f.GetProperty("filterType").GetString();
                    if (type == "PRICE_FILTER")
                    {
                        if (f.TryGetProperty("tickSize", out var ts))
                            tick = ParseInv(ts.GetString()!);
                        if (f.TryGetProperty("minPrice", out var mp))
                            minPrice = ParseInv(mp.GetString()!);
                        if (f.TryGetProperty("maxPrice", out var xp))
                            maxPrice = ParseInv(xp.GetString()!);
                    }
                    else if (type == "LOT_SIZE")
                    {
                        if (f.TryGetProperty("stepSize", out var ss))
                            step = ParseInv(ss.GetString()!);
                        if (f.TryGetProperty("minQty", out var mq))
                            minQty = ParseInv(mq.GetString()!);
                    }
                    else if (type == "MIN_NOTIONAL" && f.TryGetProperty("notional", out var nt))
                    {
                        minNotional = ParseInv(nt.GetString()!);
                    }
                }

                Logger.Log($"Rules {symbol}: tick={DecimalParser.ToInvString(tick)} step={DecimalParser.ToInvString(step)} " +
                           $"minP={(minPrice.HasValue ? DecimalParser.ToInvString(minPrice.Value) : "null")} " +
                           $"maxP={(maxPrice.HasValue ? DecimalParser.ToInvString(maxPrice.Value) : "null")} " +
                           $"minQty={(minQty.HasValue ? DecimalParser.ToInvString(minQty.Value) : "null")} " +
                           $"minNotional={(minNotional.HasValue ? DecimalParser.ToInvString(minNotional.Value) : "null")} " +
                           $"; filters={filtersJson.GetRawText()}");
            }

            if (tick >= 1m)
                throw new InvalidOperationException($"Suspicious tickSize {tick} for {symbol}. Check endpoint/baseUrl.");

            var rules = new SymbolRules(tick, step, minQty, minNotional, minPrice, maxPrice, DateTime.UtcNow);
            _rulesCache[symbol] = rules;
            return rules;
        }

        private static decimal QuantizeDown(decimal value, decimal step)
        {
            if (step <= 0) return value;
            var steps = Math.Floor(value / step);
            return steps * step;
        }

        private static decimal QuantizeToTick(decimal value, decimal tick)
        {
            if (tick <= 0) return value;
            var steps = Math.Round(value / tick, MidpointRounding.AwayFromZero);
            return steps * tick;
        }

        private async Task<(string qStr, string? pStr, string? spStr)> PrepareOrderNumbersAsync(
            string symbol, decimal quantity, decimal? price, decimal? stopPrice, bool reduceOnly, CancellationToken ct)
        {
            var r = await GetSymbolRulesAsync(symbol, ct);
            var q = r.StepSize > 0 ? QuantizeDown(quantity, r.StepSize) : quantity;
            if (q <= 0 || (r.MinQty is decimal mq && q < mq))
                throw new InvalidOperationException($"Quantity {quantity} not valid for {symbol} (step {r.StepSize}, min {r.MinQty}).");

            string qStr = DecimalParser.ToInvString(q);

            string? pStr = null;
            if (price.HasValue)
            {
                var p = r.TickSize > 0 ? QuantizeToTick(price.Value, r.TickSize) : price.Value;
                if (r.MinPrice is decimal minP && p < minP)
                    throw new InvalidOperationException($"Price {p} < minPrice {minP} for {symbol}.");
                if (r.MaxPrice is decimal maxP && p > maxP)
                    throw new InvalidOperationException($"Price {p} > maxPrice {maxP} for {symbol}.");
                pStr = DecimalParser.ToInvString(p);
            }

            string? spStr = null;
            if (stopPrice.HasValue)
            {
                var sp = r.TickSize > 0 ? QuantizeToTick(stopPrice.Value, r.TickSize) : stopPrice.Value;
                spStr = DecimalParser.ToInvString(sp);
            }

            if (!reduceOnly && r.MinNotional is decimal mn && price.HasValue)
            {
                var notion = (decimal.Parse(qStr, NumberStyles.Float, CultureInfo.InvariantCulture)) *
                             (decimal.Parse(pStr!, NumberStyles.Float, CultureInfo.InvariantCulture));
                if (notion < mn) throw new InvalidOperationException($"Notional {notion} < minNotional {mn} for {symbol}.");
            }

            return (qStr, pStr, spStr);
        }

        public async Task PlaceOrderAsync(string symbol, string side, string type, decimal quantity, decimal? price = null, bool reduceOnly = false, string? positionSide = null, string? timeInForce = null, decimal? stopPrice = null, decimal? activationPrice = null)
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["side"] = side,
                ["type"] = type
            };

            var prep = await PrepareOrderNumbersAsync(symbol, quantity, price, stopPrice, reduceOnly, CancellationToken.None);
            parameters["quantity"] = prep.qStr;
            if (prep.pStr != null) parameters["price"] = prep.pStr;
            if (prep.spStr != null) parameters["stopPrice"] = prep.spStr;

            var symbolRules = await GetSymbolRulesAsync(symbol);
            Logger.Log($"DEBUG ORDER symbol={symbol} side={side} type={type} qtyStr={prep.qStr} priceStr={prep.pStr} stepSize={DecimalParser.ToInvString(symbolRules.StepSize)} tickSize={DecimalParser.ToInvString(symbolRules.TickSize)}");

            if (string.IsNullOrEmpty(timeInForce) && type.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
                parameters["timeInForce"] = "GTC";
            else if (!string.IsNullOrEmpty(timeInForce))
                parameters["timeInForce"] = timeInForce;

            if (reduceOnly) parameters["reduceOnly"] = "true";
            if (!string.IsNullOrEmpty(positionSide)) parameters["positionSide"] = positionSide;

            if (activationPrice.HasValue)
            {
                var ap = symbolRules.TickSize > 0 ? QuantizeToTick(activationPrice.Value, symbolRules.TickSize) : activationPrice.Value;
                parameters["activationPrice"] = DecimalParser.ToInvString(ap);
            }

            var testParams = new Dictionary<string, string>(parameters);
            try
            {
                await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order/test", testParams);
            }
            catch (HttpRequestException ex)
            {
                Logger.Log($"ORDER TEST ERROR: {ex.Message}");
                throw;
            }

            Logger.Log($"ORDER SEND symbol={symbol} side={side} type={type} reduceOnly={reduceOnly} positionSide={positionSide} qty={prep.qStr} price={prep.pStr} stopPrice={prep.spStr} timeInForce={(parameters.TryGetValue("timeInForce", out var tif) ? tif : null)}");

            try
            {
                await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", parameters);
            }
            catch (HttpRequestException ex)
            {
                Logger.Log($"ORDER ERROR: {ex.Message}");
                throw;
            }
        }

        

        /// <summary>
        /// Hesaptaki açık pozisyonları döner.
        /// </summary>
        public async Task<IList<FuturesPosition>> GetOpenPositionsAsync()
        {
            // Fetch isolated wallet values for each symbol using the account endpoint.
            var wallets = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var accJson = await GetAccountInfoAsync();
                using var accDoc = JsonDocument.Parse(accJson);
                if (accDoc.RootElement.TryGetProperty("positions", out var accPositions))
                {
                    foreach (var p in accPositions.EnumerateArray())
                    {
                        var sym = p.GetProperty("symbol").GetString() ?? string.Empty;
                        if (decimal.TryParse(p.GetProperty("isolatedWallet").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var wallet))
                            wallets[sym] = wallet;
                    }
                }
            }
            catch { }

            var list = await GetPositionRiskV2Async(null);
            var positions = new List<FuturesPosition>();

            foreach (var el in list)
            {
                var amt = el.PositionAmt;
                if (amt != 0m)
                {
                    var sym = el.Symbol;
                    var levEff = el.Leverage == 0 ? 1 : el.Leverage;
                    var margin = wallets.TryGetValue(sym, out var wallet) ? Math.Abs(wallet) : 0m;

                    positions.Add(new FuturesPosition
                    {
                        Symbol = sym,
                        PositionSide = el.PositionSideRaw,
                        PositionAmt = amt,
                        EntryPrice = el.EntryPrice,
                        UnrealizedPnl = el.UnrealizedPnl,
                        MarkPrice = el.MarkPrice,
                        LiquidationPrice = el.LiquidationPrice,
                        Leverage = levEff,
                        MarginType = el.MarginType,
                        EntryAmount = margin
                    });
                }
            }

            return positions;
        }

    }
}
