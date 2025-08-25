using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
