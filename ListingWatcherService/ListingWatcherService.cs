using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace ListingWatcher;

/// <summary>
/// Background worker that polls exchange listing APIs and logs new items.
/// </summary>
public sealed class ListingWatcherService : BackgroundService
{
    private readonly ILogger<ListingWatcherService> _logger;
    private readonly HttpClient _http;
    //private readonly ConcurrentDictionary<string, byte> _seen = new();
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
    private readonly string _connectionString;
    private readonly Channel<ListingItem> _queue = Channel.CreateUnbounded<ListingItem>();
    private readonly HashSet<string> _binanceSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISymbolExtractor _symbolExtractor;
    private readonly string? _translatorKey;
    private readonly string? _translatorRegion;

    public ListingWatcherService(
        ILogger<ListingWatcherService> logger,
        ISymbolExtractor symbolExtractor,
        IConfiguration configuration)
    {
        _logger = logger;
        _symbolExtractor = symbolExtractor;

        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ListingWatcher/1.0");

        _connectionString = configuration.GetConnectionString("Listings") ??
            LoadConnectionStringFromSettings();

        var translatorKey = configuration["AZURE_TRANSLATOR_KEY"] ??
            Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_KEY");
        var translatorRegion = configuration["AZURE_TRANSLATOR_REGION"] ??
            Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_REGION");

        if (string.IsNullOrEmpty(translatorKey) || string.IsNullOrEmpty(translatorRegion))
        {
            try
            {
                var appDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BinanceUsdtTicker");
                var settingsPath = Path.Combine(appDir, "ui_settings.json");
                if (!File.Exists(settingsPath))
                    settingsPath = Path.Combine(appDir, "ui_defaults.json");
                if (File.Exists(settingsPath))
                {
                    using var stream = File.OpenRead(settingsPath);
                    using var doc = JsonDocument.Parse(stream);
                    if (string.IsNullOrEmpty(translatorKey) &&
                        doc.RootElement.TryGetProperty("TranslateKey", out var keyEl))
                        translatorKey = keyEl.GetString();
                    if (string.IsNullOrEmpty(translatorRegion) &&
                        doc.RootElement.TryGetProperty("TranslateRegion", out var regionEl))
                        translatorRegion = regionEl.GetString();
                }
            }
            catch
            {
                // ignore config file errors
            }
        }

        _translatorKey = translatorKey;
        _translatorRegion = translatorRegion;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseAsync(stoppingToken);
        await LoadBinanceSymbolsAsync(stoppingToken);

        var tasks = new[]
        {
            RunPollingLoop("bybit", PollBybitAsync, stoppingToken),
            RunPollingLoop("kucoin", PollKucoinAsync, stoppingToken),
            RunPollingLoop("okx", PollOkxAsync, stoppingToken),
            SaveWorkerAsync(stoppingToken)
        };

        await Task.WhenAll(tasks);
    }

    private async Task LoadBinanceSymbolsAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync("https://fapi.binance.com/fapi/v1/exchangeInfo", ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("symbols", out var symbolsEl))
            {
                foreach (var el in symbolsEl.EnumerateArray())
                {
                    if (el.TryGetProperty("quoteAsset", out var quoteEl) &&
                        string.Equals(quoteEl.GetString(), "USDT", StringComparison.OrdinalIgnoreCase) &&
                        el.TryGetProperty("status", out var statusEl) &&
                        string.Equals(statusEl.GetString(), "TRADING", StringComparison.OrdinalIgnoreCase))
                    {
                        var sym = el.GetProperty("symbol").GetString();
                        if (!string.IsNullOrEmpty(sym))
                            _binanceSymbols.Add(sym);
                    }
                }
            }
            _logger.LogInformation("loaded {Count} binance usdt futures symbols", _binanceSymbols.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to load binance symbols");
        }
    }

    private async Task SaveWorkerAsync(CancellationToken ct)
    {
        var reader = _queue.Reader;
        _logger.LogInformation("save worker started");


        try
        {
            while (await reader.WaitToReadAsync(ct))
            {
                while (reader.TryRead(out var item))
                {
                    try
                    {
                        await SaveListingAsync(item.Source, item.Id, item.Title, item.Url, item.Symbol, item.CreatedAt);
                    }
                    catch (SqlException ex) when (ex.Number is 2627 or 2601)
                    {
                        // duplicate → atla
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "save failed for {Source}/{Id}", item.Source, item.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal kapanış
        }
        finally
        {
            _logger.LogInformation("save worker stopped");
        }
    }

    private async Task RunPollingLoop(string name, Func<CancellationToken, Task> poll, CancellationToken ct)
    {
        // 5 saniyelik sabit periyot. PeriodicTimer drift'i azaltır
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _logger.LogInformation("{Name} loop started (5s)", name);


        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    _logger.LogDebug("tick {Name} {Time}", name, DateTimeOffset.Now);
                    await poll(ct); // Bybit/KuCoin/OKX tek tur çalışır
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Host durdurdu, çıkıyoruz
                    break;
                }
                catch (Exception ex)
                {
                    // Her türlü hata loglanır, döngü devam eder
                    _logger.LogError(ex, "{Name} polling failed", name);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal kapanış
        }
        finally
        {
            _logger.LogInformation("{Name} loop stopped", name);
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
            _logger.LogInformation("Bybit new listing: {Title} {Url}", title, urlItem);
            // Bybit exposes the announcement timestamp as "dateTimestamp" which may
            // be a number or a string depending on the API version.
            var cTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (el.TryGetProperty("dateTimestamp", out var pTime))
            {
                if (pTime.ValueKind == JsonValueKind.Number && pTime.TryGetInt64(out var tNum))
                {
                    cTimeMs = tNum;
                }
                else if (pTime.ValueKind == JsonValueKind.String && long.TryParse(pTime.GetString(), out var tStr))
                {
                    cTimeMs = tStr;
                }
            }

            await ProcessListingAsync("bybit", stableId, title, urlItem,
                DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs), ct);
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


            var title = el.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
            var urlItem = el.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
            _logger.LogInformation("KuCoin new listing: {Title} {Url}", title, urlItem);
            var cTimeMs = el.TryGetProperty("cTime", out var pTime) ? pTime.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await ProcessListingAsync("kucoin", id, title, urlItem, DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs), ct);

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

                var title = el.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                var urlItem = el.TryGetProperty("url", out pUrl) ? pUrl.GetString() : null;
                _logger.LogInformation("OKX new listing: {Title} {Url}", title, urlItem);
                var cTimeMs = el.TryGetProperty("pTime", out var pTime) && long.TryParse(pTime.GetString(), out var t)
                    ? t
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await ProcessListingAsync("okx", id, title, urlItem, DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs), ct);
            }
        }
    }

    private async Task ProcessListingAsync(string source, string id, string title, string? url, DateTimeOffset createdAt, CancellationToken ct)
    {
        var symbols = _symbolExtractor.ExtractUsdtPairs(title)
            .Where(s => _binanceSymbols.Contains(s))
            .ToList();
        if (symbols.Count == 0)
            return;
        var normalizedId = NormalizeId(id);
        foreach (var sym in symbols)
        {
            var item = new ListingItem(source, normalizedId + ":" + sym, title, url, sym, createdAt);
            await _queue.Writer.WriteAsync(item, ct);
        }
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
        if (string.IsNullOrEmpty(_connectionString))
            return;
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
        TitleTranslate NVARCHAR(MAX) NOT NULL,
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

    private async Task<string> TranslateTitleAsync(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrEmpty(_translatorKey))
            return title;

        try
        {
            var placeholders = new Dictionary<string, string>();
            var prepared = ReplaceSymbolsWithPlaceholders(title, placeholders);

            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to=tr");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _translatorKey);
            if (!string.IsNullOrEmpty(_translatorRegion))
                request.Headers.Add("Ocp-Apim-Subscription-Region", _translatorRegion);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "translation failed for {Title}", title);
            return title;
        }
    }

    private static string ReplaceSymbolsWithPlaceholders(string text, IDictionary<string, string> map)
    {
        int index = 0;

        // Protect tokens inside parentheses, e.g. (PROVE)
        var result = Regex.Replace(text, @"\(([\p{Lu}0-9]+)\)", match =>
        {
            var token = match.Groups[1].Value;
            var placeholder = $"__{index++}__";
            map[placeholder] = token;
            return "(" + placeholder + ")";
        });

        // Protect standalone uppercase tokens (e.g. BTC, PROVE)
        result = Regex.Replace(result, @"\b[\p{Lu}0-9]{2,}\b", match =>
        {
            var token = match.Value;
            var placeholder = $"__{index++}__";
            map[placeholder] = token;
            return placeholder;
        });

        return result;
    }

    private static string RestorePlaceholders(string text, IDictionary<string, string> map)
    {
        foreach (var kvp in map)
            text = text.Replace(kvp.Key, kvp.Value);
        return text;
    }

    private async Task SaveListingAsync(string source, string id, string title, string? url, string symbol, DateTimeOffset createdAt)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return;
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var check = conn.CreateCommand();
            check.CommandText = "SELECT 1 FROM dbo.News WHERE Id = @Id";
            check.Parameters.AddWithValue("@Id", id);
            var exists = await check.ExecuteScalarAsync();
            if (exists != null)
                return;

            var translated = await TranslateTitleAsync(title);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO dbo.News (Id, Source, Title, TitleTranslate, Url, Symbols, CreatedAt)
VALUES (@Id, @Source, @Title, @TitleTranslate, @Url, @Symbols, @CreatedAt);";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Source", source);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@TitleTranslate", translated);
            cmd.Parameters.AddWithValue("@Url", (object?)url ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Symbols", symbol);
            var localCreatedAt = TimeZoneInfo.ConvertTime(createdAt, TurkeyTimeZone);
            cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = localCreatedAt;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB insert failed");
        }
    }

    private static string LoadConnectionStringFromSettings()
    {
        try
        {
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceUsdtTicker");
            var settingsPath = Path.Combine(appDir, "ui_settings.json");
            if (!File.Exists(settingsPath))
                settingsPath = Path.Combine(appDir, "ui_defaults.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<UiSettings>(json);
                if (settings != null)
                    return settings.GetConnectionString();
            }
        }
        catch { }
        return string.Empty;
    }

    private record ListingItem(string Source, string Id, string Title, string? Url, string Symbol, DateTimeOffset CreatedAt);

}
