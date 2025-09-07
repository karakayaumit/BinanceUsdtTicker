using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Simple Azure Translator client that preserves symbols like (PROVE)
    /// by replacing them with placeholders during translation.
    /// </summary>
    public class AzureTranslator
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;
        private readonly string? _region;

        public AzureTranslator(string apiKey, string? region = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _region = region;
        }

        public async Task<string> TranslateAsync(string text, string to)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var placeholders = new Dictionary<string, string>();
            var prepared = ReplaceSymbolsWithPlaceholders(text, placeholders);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to={to}");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            if (!string.IsNullOrEmpty(_region))
                request.Headers.Add("Ocp-Apim-Subscription-Region", _region);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var body = JsonSerializer.Serialize(new object[] { new { text = prepared } });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(request);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var translated = doc.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString() ?? string.Empty;

            return RestorePlaceholders(translated, placeholders);
        }

        private static string ReplaceSymbolsWithPlaceholders(string text, IDictionary<string, string> map)
        {
            int index = 0;
            return Regex.Replace(text, @"\(([A-Z0-9]+)\)", match =>
            {
                var token = match.Groups[1].Value;
                var placeholder = $"__SYMBOL_{index++}__";
                map[placeholder] = token;
                return "(" + placeholder + ")";
            });
        }

        private static string RestorePlaceholders(string text, IDictionary<string, string> map)
        {
            foreach (var kvp in map)
                text = text.Replace(kvp.Key, kvp.Value);
            return text;
        }
    }
}
