using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Binance Futures REST çağrıları için ortak imzalama ve gönderim mantığını içerir.
    /// </summary>
    public abstract class BinanceRestClientBase
    {
        protected readonly HttpClient _http;
        private byte[] _secretKeyBytes = Array.Empty<byte>();
        private string _apiKey = string.Empty;

        protected BinanceRestClientBase(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// API anahtarı ve gizli anahtar ataması yapar.
        /// </summary>
        public void SetApiCredentials(string apiKey, string secretKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _secretKeyBytes = string.IsNullOrEmpty(secretKey)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(secretKey);
        }

        /// <summary>
        /// İmzalı isteği gönderir. Timestamp otomatik olarak eklenir.
        /// </summary>
        protected async Task<string> SendSignedAsync(HttpMethod method, string endpoint, IDictionary<string, string>? parameters = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey) || _secretKeyBytes.Length == 0)
                throw new InvalidOperationException("API bilgileri ayarlanmadı.");

            parameters ??= new Dictionary<string, string>();
            if (!parameters.ContainsKey("timestamp"))
                parameters["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            var queryString = string.Join("&", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            var signature = Sign(queryString);
            var url = endpoint + "?" + queryString + "&signature=" + signature;

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-MBX-APIKEY", _apiKey);
            using var response = await _http.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Content: {content}");
            }

            return content;
        }

        /// <summary>
        /// İmza gerektirmeyen basit isteği gönderir.
        /// </summary>
        protected async Task<string> SendAsync(HttpMethod method, string endpoint, CancellationToken ct = default)
        {
            using var response = await _http.SendAsync(new HttpRequestMessage(method, endpoint), ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private string Sign(string queryString)
        {
            var data = Encoding.UTF8.GetBytes(queryString);
            var hash = HMACSHA256.HashData(_secretKeyBytes, data);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}

