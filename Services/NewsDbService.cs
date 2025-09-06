using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace BinanceUsdtTicker
{
    public class NewsDbService : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly TimeSpan _pollInterval;
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

        public event EventHandler<NewsItem>? NewsReceived;

        public NewsDbService(string connectionString, TimeSpan pollInterval)
        {
            _connectionString = connectionString;
            _pollInterval = pollInterval;
        }

        public Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => PollAsync(_cts.Token));
            return Task.CompletedTask;
        }

        private async Task PollAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync(ct);
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"SELECT Id, Source, Title, Url, Symbols, CreatedAt FROM dbo.News WHERE CreatedAt > @last ORDER BY CreatedAt";
                    cmd.Parameters.AddWithValue("@last", _lastTimestamp);

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var id = reader.GetString(0);
                        var source = reader.GetString(1);
                        var title = reader.GetString(2);
                        var url = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                        var symbolsStr = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                        var created = reader.GetDateTimeOffset(5);
                        var symbols = symbolsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        var item = new NewsItem(id: id, source: source, timestamp: created.UtcDateTime, title: title, body: null, link: url, type: NewsType.Listing, symbols: symbols);
                        NewsReceived?.Invoke(this, item);
                        if (created > _lastTimestamp)
                            _lastTimestamp = created;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"DB news poll error: {ex}");
                }

                try
                {
                    await Task.Delay(_pollInterval, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_loop != null)
                {
                    try { await _loop; } catch { }
                }
                _cts.Dispose();
            }
        }
    }
}
