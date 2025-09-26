using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        private const int DefaultRecvWindowMs = 60000;
        private static readonly TimeSpan TimeSyncInterval = TimeSpan.FromMinutes(5);
        private static readonly string TimestampErrorMessage = "Timestamp for this request is outside of the recvWindow";
        private byte[] _secretKeyBytes = Array.Empty<byte>();
        private string _apiKey = string.Empty;
        private long _timeOffsetMs;
        private DateTime _lastServerTimeSyncUtc = DateTime.MinValue;
        private readonly SemaphoreSlim _timeSyncLock = new(1, 1);

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
            if (!parameters.ContainsKey("recvWindow"))
                parameters["recvWindow"] = DefaultRecvWindowMs.ToString(CultureInfo.InvariantCulture);

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await EnsureTimeSyncedAsync(ct);
                parameters["timestamp"] = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Interlocked.Read(ref _timeOffsetMs))
                    .ToString(CultureInfo.InvariantCulture);

                var queryString = BuildQuery(parameters);
                var signature = Sign(queryString);
                var url = endpoint + "?" + queryString + "&signature=" + signature;

                using var request = new HttpRequestMessage(method, url);
                request.Headers.Add("X-MBX-APIKEY", _apiKey);
                using var response = await _http.SendAsync(request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return content;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log(content);

                    if (content.Contains(TimestampErrorMessage, StringComparison.OrdinalIgnoreCase) && attempt == 0)
                    {
                        ResetTimeSync();
                        continue;
                    }

                    var message = content;
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("msg", out var msgEl))
                            message = msgEl.GetString() ?? content;
                    }
                    catch { }
                    throw new HttpRequestException(message);
                }

                return content;
            }

            throw new HttpRequestException("İstek başarısız oldu.");
        }

        /// <summary>
        /// İmza gerektirmeyen basit isteği gönderir.
        /// </summary>
        protected async Task<string> SendAsync(HttpMethod method, string endpoint, CancellationToken ct = default)
        {
            using var response = await _http.SendAsync(new HttpRequestMessage(method, endpoint), ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return await response.Content.ReadAsStringAsync(ct);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private string Sign(string queryString)
        {
            var data = Encoding.UTF8.GetBytes(queryString);
            var hash = HMACSHA256.HashData(_secretKeyBytes, data);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private async Task EnsureTimeSyncedAsync(CancellationToken ct)
        {
            if (DateTime.UtcNow - _lastServerTimeSyncUtc < TimeSyncInterval)
                return;

            await SyncServerTimeAsync(ct);
        }

        private async Task SyncServerTimeAsync(CancellationToken ct)
        {
            await _timeSyncLock.WaitAsync(ct);
            try
            {
                if (DateTime.UtcNow - _lastServerTimeSyncUtc < TimeSyncInterval)
                    return;

                using var response = await _http.GetAsync("/fapi/v1/time", ct);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(content);
                var serverTime = doc.RootElement.GetProperty("serverTime").GetInt64();
                var localTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Interlocked.Exchange(ref _timeOffsetMs, serverTime - localTime);
                _lastServerTimeSyncUtc = DateTime.UtcNow;
            }
            finally
            {
                _timeSyncLock.Release();
            }
        }

        private void ResetTimeSync()
        {
            _lastServerTimeSyncUtc = DateTime.MinValue;
            Interlocked.Exchange(ref _timeOffsetMs, 0);
        }

        private static string BuildQuery(IDictionary<string, string> p)
        {
            return string.Join("&", p.Select(kv => $"{kv.Key}={Uri.EscapeDataString(Normalize(kv.Value))}"));
        }

        private static string Normalize(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec) ||
                decimal.TryParse(value, NumberStyles.Number, new CultureInfo("tr-TR"), out dec))
                return dec.ToString("0.####################", CultureInfo.InvariantCulture);
            return value;
        }
    }
}

