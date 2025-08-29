using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using BinanceUsdtTicker.Models;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Basit Binance Futures REST API istemcisi. API anahtarı ve gizli anahtarla imzalı istek gönderir.
    /// </summary>
    public class BinanceApiService
    {
        private readonly HttpClient _http;
        private string _apiKey = string.Empty;
        private string _secretKey = string.Empty;

        private readonly Dictionary<string, (decimal TickSize, decimal StepSize)> _symbolFilters = new(StringComparer.OrdinalIgnoreCase);

        public BinanceApiService()
        {
            _http = new HttpClient { BaseAddress = new Uri("https://fapi.binance.com") };
        }

        public void SetApiCredentials(string apiKey, string secretKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _secretKey = secretKey ?? string.Empty;
        }

        /// <summary>
        /// Örnek amaçlı hesap bilgilerini döner.
        /// </summary>
        public async Task<string> GetAccountInfoAsync()
        {
            var query = new Dictionary<string, string>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };
            return await SendSignedAsync(HttpMethod.Get, "/fapi/v2/account", query);
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
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol
            };

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v2/positionRisk", query);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var el = doc.RootElement.EnumerateArray().FirstOrDefault();
                var mt = el.GetProperty("marginType").GetString() ?? "cross";
                int.TryParse(el.GetProperty("leverage").GetString(), out var lev);
                return (mt.ToLowerInvariant(), lev);
            }
            catch
            {
                return ("cross", 1);
            }
        }

        /// <summary>
        /// Sembol için kullanılabilir kaldıraç değerlerini ve bakım marj oranını döner.
        /// </summary>
        public async Task<(IList<int> Options, decimal MaintMarginRatio)> GetLeverageOptionsAsync(string symbol)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol
            };

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/leverageBracket", query);
            var list = new List<int>();
            decimal mmr = 0m;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (root.TryGetProperty("brackets", out var brackets))
                {
                    int max = 0;
                    var first = brackets.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind != JsonValueKind.Undefined)
                    {
                        if (first.TryGetProperty("maintMarginRatio", out var mmrEl))
                            mmr = mmrEl.GetDecimal();
                    }

                    foreach (var br in brackets.EnumerateArray())
                    {
                        if (br.TryGetProperty("initialLeverage", out var il) && il.TryGetInt32(out var lvl))
                            if (lvl > max) max = lvl;
                    }
                    for (int i = 1; i <= max; i++) list.Add(i);
                }
            }
            catch { }
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
                ["limit"] = limit.ToString()
            };

            if (startTime.HasValue)
            {
                var ms = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
                query["startTime"] = ms.ToString();
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
                ["limit"] = limit.ToString()
            };

            if (startTime.HasValue)
            {
                var ms = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
                query["startTime"] = ms.ToString();
            }

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/allOrders", query);
            var list = new List<FuturesOrder>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                    decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesOrder
                    {
                        Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
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
                    decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                    decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesOrder
                    {
                        Symbol = el.GetProperty("symbol").GetString() ?? string.Empty,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
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
                query["orderId"] = orderId.Value.ToString();
            if (!string.IsNullOrEmpty(origClientOrderId))
                query["origClientOrderId"] = origClientOrderId;

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/openOrder", query);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var el = doc.RootElement;
                decimal.TryParse(el.GetProperty("origQty").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                long time = el.GetProperty("time").GetInt64();
                return new FuturesOrder
                {
                    Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                    Side = el.GetProperty("side").GetString() ?? string.Empty,
                    Quantity = qty,
                    Price = price,
                    Status = el.GetProperty("status").GetString() ?? string.Empty,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
                };
            }
            catch
            {
                return null;
            }
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

        private async Task<(decimal TickSize, decimal StepSize)> GetSymbolFiltersAsync(string symbol)
        {
            if (_symbolFilters.TryGetValue(symbol, out var f))
                return f;

            var json = await _http.GetStringAsync($"/fapi/v1/exchangeInfo?symbol={symbol}");
            decimal tick = 0m;
            decimal step = 0m;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var sym = doc.RootElement.GetProperty("symbols").EnumerateArray().FirstOrDefault();
                if (sym.ValueKind != JsonValueKind.Undefined && sym.TryGetProperty("filters", out var filters))
                {
                    foreach (var fl in filters.EnumerateArray())
                    {
                        var type = fl.GetProperty("filterType").GetString();
                        if (type == "PRICE_FILTER")
                        {
                            decimal.TryParse(fl.GetProperty("tickSize").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out tick);
                        }
                        else if (type == "LOT_SIZE")
                        {
                            decimal.TryParse(fl.GetProperty("stepSize").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out step);
                        }
                    }
                }
            }
            catch { }

            var res = (tick, step);
            _symbolFilters[symbol] = res;
            return res;
        }

        public async Task PlaceOrderAsync(string symbol, string side, string type, decimal quantity, decimal? price = null, bool reduceOnly = false, string? positionSide = null)
        {
            var filters = await GetSymbolFiltersAsync(symbol);
            var adjQty = filters.StepSize > 0 ? Math.Floor(quantity / filters.StepSize) * filters.StepSize : quantity;
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["side"] = side.ToUpperInvariant(),
                ["type"] = type.ToUpperInvariant(),
                ["quantity"] = adjQty.ToString(CultureInfo.InvariantCulture)
            };
            if (price.HasValue)
            {
                var adjPrice = filters.TickSize > 0 ? Math.Floor(price.Value / filters.TickSize) * filters.TickSize : price.Value;
                query["price"] = adjPrice.ToString(CultureInfo.InvariantCulture);
            }
            if (type.Equals("LIMIT", StringComparison.OrdinalIgnoreCase))
                query["timeInForce"] = "GTC";
            if (reduceOnly)
                query["reduceOnly"] = "true";
            if (!string.IsNullOrEmpty(positionSide))
                query["positionSide"] = positionSide.ToUpperInvariant();

            await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", query);
        }

        /// <summary>
        /// Hesaptaki açık pozisyonları döner.
        /// </summary>
        public async Task<IList<FuturesPosition>> GetOpenPositionsAsync()
        {
            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v3/positionRisk");
            var positions = new List<FuturesPosition>();

            // Pozisyon ayrıntılarını almak için hesap bilgisini çek.
            var details = new Dictionary<string, (int Lev, string Mt)>();
            try
            {
                var accJson = await SendSignedAsync(HttpMethod.Get, "/fapi/v2/account");
                using var docAcc = JsonDocument.Parse(accJson);
                if (docAcc.RootElement.TryGetProperty("positions", out var posArr))
                {
                    foreach (var p in posArr.EnumerateArray())
                    {
                        var sym = p.GetProperty("symbol").GetString() ?? string.Empty;
                        int.TryParse(p.GetProperty("leverage").GetString(), out var lev);
                        var mt = p.GetProperty("marginType").GetString() ?? "cross";
                        details[sym] = (lev, mt);
                    }
                }
            }
            catch { }

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    decimal.TryParse(el.GetProperty("positionAmt").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt);
                    decimal.TryParse(el.GetProperty("entryPrice").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var entry);
                    decimal.TryParse(el.GetProperty("unRealizedProfit").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var pnl);
                    decimal.TryParse(el.GetProperty("markPrice").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var mark);
                    decimal.TryParse(el.GetProperty("liquidationPrice").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var liq);
                    decimal margin = 0m;
                    if (el.TryGetProperty("positionInitialMargin", out var imEl))
                        decimal.TryParse(imEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out margin);
                    var sym = el.GetProperty("symbol").GetString() ?? string.Empty;
                    details.TryGetValue(sym, out var det);
                    if (amt != 0m)
                    {
                        positions.Add(new FuturesPosition
                        {
                            Symbol = sym,
                            PositionAmt = amt,
                            EntryPrice = entry,
                            UnrealizedPnl = pnl,
                            MarkPrice = mark,
                            LiquidationPrice = liq,
                            Leverage = det.Lev,
                            MarginType = det.Mt,
                            InitialMargin = margin
                        });
                    }
                }
            }
            catch { }

            return positions;
        }

        public async Task<string> SendSignedAsync(HttpMethod method, string endpoint, IDictionary<string, string>? parameters = null)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                throw new InvalidOperationException("API bilgileri ayarlanmadı.");

            parameters ??= new Dictionary<string, string>();
            if (!parameters.ContainsKey("timestamp"))
                parameters["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            var queryString = string.Join("&", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            var signature = Sign(queryString);
            var url = endpoint + "?" + queryString + "&signature=" + signature;

            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-MBX-APIKEY", _apiKey);
            var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Content: {content}");
            }

            return content;
        }

        private string Sign(string queryString)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
