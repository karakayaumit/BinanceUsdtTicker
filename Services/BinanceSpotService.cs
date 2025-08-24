using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    public enum WsState { Closed, Connecting, Connected, Retrying }

    /// <summary>
    /// miniTicker + bookTicker (mid price) birleşik canlı akış. (DECIMAL tabanlı)
    /// </summary>
    public class BinanceSpotService
    {
        public event Action<List<TickerRow>>? OnTickersUpdated;

        public event Action<string, Candle>? OnCandle;

        // WS durum bildirimi: (durum, deneme sayısı)
        public event Action<WsState, int>? OnWsStateChanged;

        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private Task? _runner;

        private readonly Dictionary<string, TickerRow> _state =
            new(StringComparer.OrdinalIgnoreCase);

        // Emit throttle
        private long _emitIntervalTicks = TimeSpan.FromMilliseconds(300).Ticks;
        private long _nextEmitTicks = 0;

        private readonly CandleAggregator _candleAgg = new(TimeSpan.FromMinutes(1));

        public BinanceSpotService()
        {
            _candleAgg.OnCandle += (s, c) => OnCandle?.Invoke(s, c);
        }

        public WsState State { get; private set; } = WsState.Closed;
        public DateTime LastMessageUtc { get; private set; }
        public int MessageGapMs => LastMessageUtc == default ? -1
            : (int)Math.Max(0, (DateTime.UtcNow - LastMessageUtc).TotalMilliseconds);

        private static bool IsUsdt(string s) =>
            s.EndsWith("USDT", StringComparison.OrdinalIgnoreCase);

        public async Task StartAsync()
        {
            await StopAsync();

            _cts = new CancellationTokenSource();
            _runner = Task.Run(() => ConnectLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            try { _cts?.Cancel(); } catch { }

            if (_ws != null)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None); } catch { }
                _ws.Dispose();
                _ws = null;
            }

            _cts?.Dispose(); _cts = null;
            State = WsState.Closed;
            OnWsStateChanged?.Invoke(State, 0);
        }

        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            int attempt = 0;
            var url = "wss://stream.binance.com:9443/stream?streams=!miniTicker@arr/!bookTicker@arr";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    State = attempt == 0 ? WsState.Connecting : WsState.Retrying;
                    OnWsStateChanged?.Invoke(State, attempt);

                    using var ws = new ClientWebSocket();
                    _ws = ws;

                    await ws.ConnectAsync(new Uri(url), ct);
                    State = WsState.Connected;
                    attempt = 0;
                    OnWsStateChanged?.Invoke(State, attempt);

                    await ReceiveLoop(ws, ct);
                }
                catch
                {
                    // düş ve tekrar dene
                }

                if (ct.IsCancellationRequested) break;

                attempt++;
                State = WsState.Retrying;
                OnWsStateChanged?.Invoke(State, attempt);

                // exponential backoff (1,2,4,8,16,30 sn)
                var sec = Math.Min(30, 1 << Math.Min(5, attempt));
                try { await Task.Delay(TimeSpan.FromSeconds(sec), ct); } catch { }
            }

            State = WsState.Closed;
            OnWsStateChanged?.Invoke(State, attempt);
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
        {
            var buf = new ArraySegment<byte>(new byte[1 << 20]); // 1 MB
            var sb = new StringBuilder(1 << 20);

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                sb.Clear();
                WebSocketReceiveResult? res;
                do
                {
                    res = await ws.ReceiveAsync(buf, ct);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buf.Array!, 0, res.Count));
                } while (!res.EndOfMessage);

                LastMessageUtc = DateTime.UtcNow;
                var json = sb.ToString();

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("stream", out var streamEl))
                    {
                        var stream = streamEl.GetString() ?? "";
                        var dataEl = root.GetProperty("data");

                        if (stream.Contains("miniTicker"))
                            HandleMiniTickerArray(dataEl);
                        else if (stream.Contains("bookTicker"))
                            HandleBookTickerArray(dataEl);
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        if (root.GetArrayLength() > 0 && root[0].TryGetProperty("e", out _))
                            HandleMiniTickerArray(root);
                        else
                            HandleBookTickerArray(root);
                    }
                }
                catch
                {
                    // parse hatalarını sessiz geç
                }

                MaybeEmit();
            }
        }

        // 24h miniTicker: c/o/h/l/v/P  (decimal okur)
        private void HandleMiniTickerArray(JsonElement arr)
        {
            var now = DateTime.UtcNow;

            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetProperty("s").GetString() ?? "";
                if (!IsUsdt(s)) continue;

                decimal price = ReadDecimal(el, "c");
                decimal open = ReadDecimal(el, "o");
                decimal high = ReadDecimal(el, "h");
                decimal low = ReadDecimal(el, "l");
                decimal vol = ReadDecimal(el, "v");
                decimal chgPctFromBinance = ReadDecimal(el, "P"); // 24s %

                decimal changePct = open > 0m
                    ? ((price - open) / open) * 100m
                    : chgPctFromBinance;

                if (!_state.TryGetValue(s, out var row))
                {
                    row = new TickerRow
                    {
                        Symbol = s,
                        Price = price,
                        Open = open,
                        High = high,
                        Low = low,
                        Volume = vol,
                        ChangePercent = changePct,
                        LastUpdate = now
                    };
                    _state[s] = row;
                }
                else
                {
                    row.Price = price;
                    row.Open = open;
                    row.High = high;
                    row.Low = low;
                    row.Volume = vol;
                    row.ChangePercent = changePct;
                    row.LastUpdate = now;
                }

                _candleAgg.AddTick(s, price, now);
            }
        }

        // bookTicker: s,b,a  → mid = (b+a)/2 (decimal)
        private void HandleBookTickerArray(JsonElement arr)
        {
            var now = DateTime.UtcNow;

            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetProperty("s").GetString() ?? "";
                if (!IsUsdt(s)) continue;

                decimal bid = ReadDecimal(el, "b");
                decimal ask = ReadDecimal(el, "a");
                if (bid <= 0m && ask <= 0m) continue;

                decimal mid = (bid > 0m && ask > 0m) ? (bid + ask) / 2m
                              : (ask > 0m ? ask : bid);

                if (!_state.TryGetValue(s, out var row))
                {
                    row = new TickerRow
                    {
                        Symbol = s,
                        Price = mid,
                        LastUpdate = now
                    };
                    _state[s] = row;
                }
                else
                {
                    row.Price = mid;
                    row.LastUpdate = now;
                }

                _candleAgg.AddTick(s, mid, now);
            }
        }

        private static decimal ReadDecimal(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number)
                {
                    var raw = v.GetRawText();
                    if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return d;
                }
                if (v.ValueKind == JsonValueKind.String)
                {
                    var s = v.GetString();
                    if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var ds))
                        return ds;
                }
            }
            return 0m;
        }

        private void MaybeEmit()
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            if (nowTicks < _nextEmitTicks) return;

            _nextEmitTicks = nowTicks + _emitIntervalTicks;
            OnTickersUpdated?.Invoke(_state.Values.ToList());
        }
    }
}
