using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace BinanceUsdtTicker.Services
{
    public enum TreeNewsEndpoint { Main, Tokyo }

    public sealed class TreeNewsOptions
    {
        public string ApiKey { get; set; } = string.Empty; // zorunlu
        public TreeNewsEndpoint Endpoint { get; set; } = TreeNewsEndpoint.Tokyo;
        public bool OnlyListings { get; set; } = true;
        public string MainWsUrl { get; set; } = "wss://news.treeofalpha.com/ws";
        public string TokyoWsUrl { get; set; } = "ws://tokyo.treeofalpha.com:5124";
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(20);
        public TimeSpan ReconnectMinDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(10);
        public Dispatcher? UiDispatcher { get; set; } = Dispatcher.CurrentDispatcher;
        public ITreeNewsLogger Logger { get; set; } = new DebugTreeNewsLogger();
        public ITreeSymbolMatcher SymbolMatcher { get; set; } = new DefaultSymbolMatcher();
    }

    public interface ITreeNewsLogger
    {
        void Info(string message);
        void Warn(string message, Exception? ex = null);
        void Error(string message, Exception? ex = null);
    }

    public sealed class DebugTreeNewsLogger : ITreeNewsLogger
    {
        public void Info(string message) => System.Diagnostics.Debug.WriteLine($"[TreeNews][INFO] {message}");
        public void Warn(string message, Exception? ex = null) => System.Diagnostics.Debug.WriteLine($"[TreeNews][WARN] {message} {ex}");
        public void Error(string message, Exception? ex = null) => System.Diagnostics.Debug.WriteLine($"[TreeNews][ERROR] {message} {ex}");
    }

    public interface ITreeSymbolMatcher
    {
        IReadOnlyList<string> MatchSymbols(in TreeNewsMessage msg);
    }

    public sealed class DefaultSymbolMatcher : ITreeSymbolMatcher
    {
        private static readonly string[] PreferredExchanges = new[]
        {
            "binance","binance-futures","bybit","bybit-perps","okx","okx-perps"
        };

        public IReadOnlyList<string> MatchSymbols(in TreeNewsMessage msg)
        {
            var list = new List<string>(4);
            if (msg.Suggestions is null) return list;
            foreach (var sug in msg.Suggestions)
            {
                if (sug.Symbols is null) continue;
                foreach (var s in sug.Symbols)
                {
                    if (s.Symbol is null) continue;
                    if (!string.IsNullOrWhiteSpace(s.Exchange) &&
                        PreferredExchanges.Contains(s.Exchange, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(s.Symbol);
                    }
                }
            }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public sealed class TreeNewsMessage
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("suggestions")] public IList<TreeSuggestion>? Suggestions { get; set; }
    }

    public sealed class TreeSuggestion
    {
        [JsonPropertyName("symbols")] public IList<TreeSymbol>? Symbols { get; set; }
    }

    public sealed class TreeSymbol
    {
        [JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("exchange")] public string? Exchange { get; set; }
    }

    public sealed class TreeNewsEventArgs : EventArgs
    {
        public TreeNewsEventArgs(TreeNewsMessage message, IReadOnlyList<string> symbols)
        {
            Message = message;
            Symbols = symbols;
        }
        public TreeNewsMessage Message { get; }
        public IReadOnlyList<string> Symbols { get; }
    }

    public sealed class TreeNewsMetrics
    {
        public int MessagesReceived { get; internal set; }
        public int ReconnectCount { get; internal set; }
    }

    public sealed class TreeTokyoNewsService : IAsyncDisposable
    {
        private ClientWebSocket _ws;
        private readonly TreeNewsOptions _options;
        private readonly CancellationTokenSource _cts = new();
        private Task? _loopTask;

        public TreeTokyoNewsService(TreeNewsOptions options)
        {
            _options = options;
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = _options.KeepAliveInterval;
        }

        public event EventHandler<TreeNewsEventArgs>? NewsReceived;
        public TreeNewsMetrics Metrics { get; } = new();

        public async Task StartAsync(CancellationToken ct = default)
        {
            await ConnectAsync(ct).ConfigureAwait(false);
            _loopTask = ReceiveLoopAsync(_cts.Token);
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            var url = _options.Endpoint == TreeNewsEndpoint.Tokyo ? _options.TokyoWsUrl : _options.MainWsUrl;
            var uri = new Uri(url);
            _options.Logger.Info($"Connecting to {uri}");
            await _ws.ConnectAsync(uri, ct).ConfigureAwait(false);
            var login = JsonSerializer.Serialize(new { api_key = _options.ApiKey, action = "login" });
            var bytes = Encoding.UTF8.GetBytes(login);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ReconnectAsync(ct).ConfigureAwait(false);
                            ms = null;
                            break;
                        }
                        ms.Write(buffer.AsSpan(0, result.Count));
                    } while (!result.EndOfMessage);
                    if (ms == null) continue;
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    ProcessMessage(json);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _options.Logger.Warn("Receive loop error", ex);
                await ReconnectAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task ReconnectAsync(CancellationToken ct)
        {
            Metrics.ReconnectCount++;
            try { _ws.Dispose(); } catch { }
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = _options.KeepAliveInterval;

            var delay = _options.ReconnectMinDelay;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    await ConnectAsync(ct).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    _options.Logger.Warn("Reconnect failed", ex);
                    var next = TimeSpan.FromMilliseconds(Math.Min(_options.ReconnectMaxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2));
                    delay = next;
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<TreeNewsMessage>(json);
                if (msg == null) return;
                if (_options.OnlyListings && msg.Category != null)
                {
                    var cat = msg.Category.ToLowerInvariant();
                    if (cat != "listing" && cat != "launchpad")
                        return;
                }
                var symbols = _options.SymbolMatcher.MatchSymbols(msg);
                if (symbols.Count == 0) return;
                Metrics.MessagesReceived++;
                if (_options.UiDispatcher != null)
                {
                    _options.UiDispatcher.BeginInvoke(() => NewsReceived?.Invoke(this, new TreeNewsEventArgs(msg, symbols)));
                }
                else
                {
                    NewsReceived?.Invoke(this, new TreeNewsEventArgs(msg, symbols));
                }
            }
            catch (Exception ex)
            {
                _options.Logger.Warn("Failed to process message", ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_loopTask != null)
                await _loopTask.ConfigureAwait(false);
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
            }
            _ws.Dispose();
            _cts.Dispose();
        }
    }
}

