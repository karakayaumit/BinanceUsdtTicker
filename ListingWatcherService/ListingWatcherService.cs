using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace ListingWatcher;

/// <summary>
/// Background worker that polls exchange listing APIs and logs new items.
/// </summary>
public sealed class ListingWatcherService : BackgroundService
{
    private readonly ILogger<ListingWatcherService> _logger;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    private static readonly Regex UsdtSym = new(@"\b([A-Z0-9]{2,15})(?:/|-)?USDTM?\b", RegexOptions.Compiled);
    private static readonly Regex ParenSym = new(@"\(([A-Z0-9]{2,15})\)", RegexOptions.Compiled);
    private readonly string _connectionString;

    public ListingWatcherService(ILogger<ListingWatcherService> logger)
    {
        _logger = logger;

        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ListingWatcher/1.0");
        _connectionString =
            Environment.GetEnvironmentVariable("BINANCE_DB_CONNECTION") ??
            "Server=KARAKAYA-MSI\\KARAKAYADB;Database=BinanceUsdtTicker;User Id=sa;Password=Lhya!812;TrustServerCertificate=True;";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseAsync(stoppingToken);

        var tasks = new[]
        {
            RunPollingLoop("bybit", PollBybitAsync, stoppingToken),
            RunPollingLoop("kucoin", PollKucoinAsync, stoppingToken),
            RunPollingLoop("okx", PollOkxAsync, stoppingToken)
        };

        await Task.WhenAll(tasks);
    }

    private async Task RunPollingLoop(string name, Func<CancellationToken, Task> poll, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await poll(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Name} polling failed", name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation to allow graceful shutdown
            }
        }
    }

    private async Task PollBybitAsync(CancellationToken ct)
    {
        // Bybit changed the listings endpoint to /v5/announcements/index.
        // Use type=new_crypto to retrieve recent listing announcements.
        // The API is cursor based, so we simply request the first page.
        var url = "https://api.bybit.com/v5/announcements/index?locale=en-US&type=new_crypto&limit=20";

        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Forbidden when polling Bybit: {Url}", url);
            return;
        }
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = doc.RootElement.GetProperty("result").GetProperty("list");
        foreach (var el in items.EnumerateArray())
        {
            // ID is exposed as either "id" or "announcementId" depending on
            // the API version. Handle both to stay compatible. However, Bybit
            // has been observed to return different ids for the same
            // announcement. Use the URL when available as a stable identifier
            // to avoid inserting duplicate rows.
            var rawId = el.TryGetProperty("id", out var pId)
                ? (pId.ValueKind == JsonValueKind.String ? pId.GetString() : pId.GetRawText()) ?? Guid.NewGuid().ToString("n")
                : el.TryGetProperty("announcementId", out var pAnnId)
                    ? (pAnnId.ValueKind == JsonValueKind.String ? pAnnId.GetString() : pAnnId.GetRawText()) ?? Guid.NewGuid().ToString("n")
                    : Guid.NewGuid().ToString("n");

            var title = el.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
            // In newer responses the URL field may be named either "url" or "link".
            var urlItem = el.TryGetProperty("url", out var pUrl)
                ? pUrl.GetString()
                : el.TryGetProperty("link", out var pLink) ? pLink.GetString() : null;

            var stableId = urlItem ?? rawId;
            if (_seen.TryAdd(stableId, 0))
            {
                _logger.LogInformation("Bybit new listing: {Title} {Url}", title, urlItem);
                await ProcessListingAsync("bybit", stableId, title, urlItem);
            }
        }
    }

    private async Task PollKucoinAsync(CancellationToken ct)
    {
        var url = "https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang=en_US&pageSize=20&currentPage=1";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Forbidden when polling KuCoin: {Url}", url);
            return;
        }
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        foreach (var el in items.EnumerateArray())
        {
            var id = el.TryGetProperty("annId", out var pId) ? pId.GetInt64().ToString() : Guid.NewGuid().ToString("n");
            if (_seen.TryAdd(id, 0))
            {
                var title = el.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                var urlItem = el.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
                _logger.LogInformation("KuCoin new listing: {Title} {Url}", title, urlItem);
                await ProcessListingAsync("kucoin", id, title, urlItem);
            }
        }
    }

    private async Task PollOkxAsync(CancellationToken ct)
    {
        var url = "https://www.okx.com/api/v5/support/announcements?annType=announcements-new-listings&page=1";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Forbidden when polling OKX: {Url}", url);
            return;
        }
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");
        foreach (var section in data.EnumerateArray())
        {
            if (!section.TryGetProperty("details", out var details))
                continue;
            foreach (var el in details.EnumerateArray())
            {
                var id = el.TryGetProperty("url", out var pUrl) ? pUrl.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
                if (_seen.TryAdd(id, 0))
                {
                    var title = el.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                    var urlItem = el.TryGetProperty("url", out pUrl) ? pUrl.GetString() : null;
                    _logger.LogInformation("OKX new listing: {Title} {Url}", title, urlItem);
                    await ProcessListingAsync("okx", id, title, urlItem);
                }
            }
        }
    }

    private async Task ProcessListingAsync(string source, string id, string title, string? url)
    {
        var symbols = ExtractSymbols(title);
        var normalizedId = NormalizeId(id);
        await SaveListingAsync(source, normalizedId, title, url, symbols);
    }

    private static IReadOnlyList<string> ExtractSymbols(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in UsdtSym.Matches(text))
            set.Add(m.Groups[1].Value + "USDT");
        foreach (Match m in ParenSym.Matches(text))
        {
            var sym = m.Groups[1].Value;
            if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                set.Add(sym);
            else if (sym.EndsWith("USDTM", StringComparison.OrdinalIgnoreCase))
                set.Add(sym.Substring(0, sym.Length - 1));
            else
                set.Add(sym + "USDT");
        }
        return set.ToList();
    }

    private static string NormalizeId(string id)
    {
        if (id.Length <= 100)
            return id;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            await using (var master = new SqlConnection(builder.ConnectionString))
            {
                await master.OpenAsync(ct);
                var cmd = master.CreateCommand();
                cmd.CommandText = "IF DB_ID(@db) IS NULL CREATE DATABASE [" + dbName + "]";
                cmd.Parameters.AddWithValue("@db", dbName);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var create = conn.CreateCommand();
            create.CommandText = @"IF OBJECT_ID('dbo.News', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.News (
        Id NVARCHAR(100) PRIMARY KEY,
        Source NVARCHAR(50) NOT NULL,
        Title NVARCHAR(MAX) NOT NULL,
        Url NVARCHAR(2048) NULL,
        Symbols NVARCHAR(200) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT (SYSDATETIMEOFFSET() AT TIME ZONE 'Turkey Standard Time')
    )
END";
            await create.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database setup failed");
        }
    }

    private async Task SaveListingAsync(string source, string id, string title, string? url, IReadOnlyList<string> symbols)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.News WHERE Id = @Id)
                    INSERT INTO dbo.News (Id, Source, Title, Url, Symbols)
                    VALUES (@Id, @Source, @Title, @Url, @Symbols);";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Source", source);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@Url", (object?)url ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Symbols", string.Join(',', symbols));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB insert failed");
        }
    }

}
