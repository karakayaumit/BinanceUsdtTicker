using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace BinanceUsdtTicker
{
    public class NewsDbService : IAsyncDisposable
    {
        private readonly string _connectionString;
        private SqlDependency? _dependency;
        private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

        public event EventHandler<NewsItem>? NewsReceived;

        public NewsDbService(string connectionString, TimeSpan _)
        {
            _connectionString = connectionString;
        }

        public async Task StartAsync()
        {
            SqlDependency.Start(_connectionString);
            await FetchAndSubscribeAsync();
        }

        private async Task FetchAndSubscribeAsync()
        {
            if (_dependency != null)
                _dependency.OnChange -= OnDependencyChange;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            if (_lastTimestamp == DateTimeOffset.MinValue)
            {
                cmd.CommandText = @"SELECT TOP 10 Id, Source, Title, TitleTranslate, Url, Symbols, CreatedAt FROM dbo.News ORDER BY CreatedAt DESC";
            }
            else
            {
                cmd.CommandText = @"SELECT Id, Source, Title, TitleTranslate, Url, Symbols, CreatedAt FROM dbo.News WHERE CreatedAt > @last ORDER BY CreatedAt";
                cmd.Parameters.AddWithValue("@last", _lastTimestamp);
            }

            _dependency = new SqlDependency(cmd);
            _dependency.OnChange += OnDependencyChange;

            await using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<(NewsItem Item, DateTimeOffset Created)>();
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var source = reader.GetString(1);
                var title = reader.GetString(2);
                var titleTranslate = reader.IsDBNull(3) ? null : reader.GetString(3);
                var url = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                var symbolsStr = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                var created = reader.GetDateTimeOffset(6);
                IReadOnlyList<string> symbols = symbolsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                var item = new NewsItem(id: id, source: source, timestamp: created.UtcDateTime, title: title, titleTranslate: titleTranslate, body: null, link: url, type: NewsType.Listing, symbols: symbols);
                items.Add((item, created));
            }

            if (_lastTimestamp == DateTimeOffset.MinValue)
                items.Reverse();

            foreach (var (item, created) in items)
            {
                NewsReceived?.Invoke(this, item);
                if (created > _lastTimestamp)
                    _lastTimestamp = created;
            }
        }

        private async void OnDependencyChange(object? sender, SqlNotificationEventArgs e)
        {
            await FetchAndSubscribeAsync();
        }

        public ValueTask DisposeAsync()
        {
            if (_dependency != null)
            {
                _dependency.OnChange -= OnDependencyChange;
                _dependency = null;
            }
            SqlDependency.Stop(_connectionString);
            return ValueTask.CompletedTask;
        }
    }
}
