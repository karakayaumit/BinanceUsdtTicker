using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace BinanceUsdtTicker;

public class BinanceSpotService
{
    private static readonly HttpClient _http = new HttpClient
    {
        BaseAddress = new Uri("https://api.binance.com")
    };

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<HashSet<string>> GetActiveUsdtSymbolsAsync(CancellationToken ct = default)
    {
        // Spot exchangeInfo (TRADING durumundaki pariteler)
        var url = "/api/v3/exchangeInfo?permissions=SPOT&symbolStatus=TRADING";
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sym in doc.RootElement.GetProperty("symbols").EnumerateArray())
        {
            var quote = sym.GetProperty("quoteAsset").GetString();
            var symbol = sym.GetProperty("symbol").GetString();
            if (quote == "USDT" && symbol is not null)
                set.Add(symbol);
        }
        return set;
    }

    public async IAsyncEnumerable<MiniTicker> StreamAllMiniTickersAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var url = new Uri("wss://stream.binance.com:9443/stream?streams=!miniTicker@arr");
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(url, ct);

        var buffer = new byte[1 << 16];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                yield break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            using var doc = JsonDocument.Parse(message);
            var data = doc.RootElement.GetProperty("data");

            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var mt = new MiniTicker
                    {
                        EventType = item.GetProperty("e").GetString() ?? "24hrMiniTicker",
                        EventTime = item.GetProperty("E").GetInt64(),
                        Symbol = item.GetProperty("s").GetString() ?? string.Empty,
                        Close = double.TryParse(item.GetProperty("c").GetString(), out var c) ? c : 0,
                        Open  = double.TryParse(item.GetProperty("o").GetString(), out var o) ? o : 0,
                        High  = double.TryParse(item.GetProperty("h").GetString(), out var h) ? h : 0,
                        Low   = double.TryParse(item.GetProperty("l").GetString(), out var l) ? l : 0,
                        Volume= double.TryParse(item.GetProperty("v").GetString(), out var v) ? v : 0,
                        QuoteVolume = double.TryParse(item.GetProperty("q").GetString(), out var q) ? q : 0,
                    };
                    yield return mt;
                }
            }
        }
    }
}

public record MiniTicker
{
    public string EventType { get; init; } = "";
    public long EventTime { get; init; }
    public string Symbol { get; init; } = "";
    public double Close { get; init; }
    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Volume { get; init; }
    public double QuoteVolume { get; init; }
}
