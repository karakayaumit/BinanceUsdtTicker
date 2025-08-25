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
            }
            catch { }
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
        /// Sembol için kullanılabilir kaldıraç değerlerini döner.
        /// </summary>
        public async Task<IList<int>> GetLeverageOptionsAsync(string symbol)
        {
            var query = new Dictionary<string, string>
            {
                ["symbol"] = symbol
            };

            var json = await SendSignedAsync(HttpMethod.Get, "/fapi/v1/leverageBracket", query);
            var list = new List<int>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (root.TryGetProperty("brackets", out var brackets))
                {
                    int max = 0;
                    foreach (var br in brackets.EnumerateArray())
                    {
                        if (br.TryGetProperty("initialLeverage", out var il) && il.TryGetInt32(out var lvl))
                            if (lvl > max) max = lvl;
                    }
                    for (int i = 1; i <= max; i++) list.Add(i);
                }
            }
            catch { }
            return list;
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
                    long time = el.GetProperty("time").GetInt64();
                    list.Add(new FuturesTrade
                    {
                        Symbol = el.GetProperty("symbol").GetString() ?? symbol,
                        Side = el.GetProperty("side").GetString() ?? string.Empty,
                        Quantity = qty,
                        Price = price,
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
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private string Sign(string queryString)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
