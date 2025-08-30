using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Xml;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
{
    Timeout = TimeSpan.FromSeconds(10)
};
http.DefaultRequestHeaders.UserAgent.ParseAdd("ExchangeNewListingsRSS/1.0");

// map KuCoin, Bybit and OKX endpoints using shared helpers
MapRssEndpoint(
    "/rss/kucoin-new",
    defaultLang: "en_US",
    channelTitle: "KuCoin – New Listings (Unofficial RSS)",
    channelLink: "https://www.kucoin.com/announcement/new-listings",
    generator: "KucoinNewListingsRSS/1.0",
    descFactory: lang => $"New Listings announcements via KuCoin API (lang={lang})",
    fetch: async (ctx, lang, pageSize, page) =>
    {
        var url = $"https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang={Uri.EscapeDataString(lang)}&pageSize={pageSize}&currentPage={page}";
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ctx.RequestAborted);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.RequestAborted);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");

        var list = new List<FeedItem>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("annId", out var pId) ? pId.GetInt64().ToString() : Guid.NewGuid().ToString("n");
            var title = it.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
            var desc = it.TryGetProperty("annDesc", out var pDesc) ? pDesc.GetString() : null;
            var urlItem = it.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
            var cTimeMs = it.TryGetProperty("cTime", out var pTime) ? pTime.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            list.Add(new FeedItem(id, title, desc, urlItem, DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs)));
        }
        return list;
    });

MapRssEndpoint(
    "/rss/bybit-new",
    defaultLang: "en-US",
    channelTitle: "Bybit – New Listings (Unofficial RSS)",
    channelLink: "https://announcement.bybit.com/en-US/?category=Listing",
    generator: "BybitNewListingsRSS/1.0",
    descFactory: lang => $"New Listings announcements via Bybit API (lang={lang})",
    fetch: async (ctx, lang, pageSize, page) =>
    {
        var url = $"https://api.bybit.com/v5/public/announcements?locale={Uri.EscapeDataString(lang)}&category=listing&pageSize={pageSize}&page={page}";
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ctx.RequestAborted);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.RequestAborted);
        var items = doc.RootElement.GetProperty("result").GetProperty("list");

        var list = new List<FeedItem>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var pId) ? pId.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
            var title = it.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
            var desc = it.TryGetProperty("description", out var pDesc) ? pDesc.GetString() : null;
            var urlItem = it.TryGetProperty("link", out var pUrl) ? pUrl.GetString() : null;
            var cTimeMs = it.TryGetProperty("createdAt", out var pTime) && long.TryParse(pTime.GetString(), out var t) ? t : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            list.Add(new FeedItem(id, title, desc, urlItem, DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs)));
        }
        return list;
    });

MapRssEndpoint(
    "/rss/okx-new",
    defaultLang: "en-US",
    channelTitle: "OKX – New Listings (Unofficial RSS)",
    channelLink: "https://www.okx.com/announcements?type=listing",
    generator: "OkxNewListingsRSS/1.0",
    descFactory: lang => $"New Listings announcements via OKX API (lang={lang})",
    fetch: async (ctx, lang, pageSize, page) =>
    {
        var url = $"https://www.okx.com/api/v5/public/announcements?lang={Uri.EscapeDataString(lang)}&category=listing&pageSize={pageSize}&page={page}";
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ctx.RequestAborted);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.RequestAborted);
        var items = doc.RootElement.GetProperty("data");

        var list = new List<FeedItem>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var pId) ? pId.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
            var title = it.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
            var desc = it.TryGetProperty("content", out var pDesc) ? pDesc.GetString() : null;
            var urlItem = it.TryGetProperty("url", out var pUrl) ? pUrl.GetString() : null;
            var cTimeMs = it.TryGetProperty("publishTime", out var pTime) && long.TryParse(pTime.GetString(), out var t) ? t : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            list.Add(new FeedItem(id, title, desc, urlItem, DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs)));
        }
        return list;
    });

app.Run();

static (string lang, int pageSize, int page) GetQuery(HttpContext ctx, string defaultLang)
{
    var lang = ctx.Request.Query["lang"].FirstOrDefault() ?? defaultLang;
    var pageSize = int.TryParse(ctx.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? Math.Clamp(ps, 1, 50) : 20;
    var page = int.TryParse(ctx.Request.Query["page"].FirstOrDefault(), out var pg) ? Math.Max(pg, 1) : 1;
    return (lang, pageSize, page);
}

static void MapRssEndpoint(
    string route,
    string defaultLang,
    string channelTitle,
    string channelLink,
    string generator,
    Func<string, string> descFactory,
    Func<HttpContext, string, int, int, Task<IEnumerable<FeedItem>>> fetch)
{
    app.MapGet(route, async ctx =>
    {
        var (lang, pageSize, page) = GetQuery(ctx, defaultLang);
        var items = await fetch(ctx, lang, pageSize, page);
        await WriteFeed(ctx, channelTitle, channelLink, descFactory(lang), generator, items);
    });
}

static async Task WriteFeed(HttpContext ctx, string title, string link, string desc, string generator, IEnumerable<FeedItem> items)
{
    ctx.Response.ContentType = "application/rss+xml; charset=utf-8";
    var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
    using var xw = XmlWriter.Create(ctx.Response.Body, settings);

    xw.WriteStartDocument();
    xw.WriteStartElement("rss");
    xw.WriteAttributeString("version", "2.0");
    xw.WriteStartElement("channel");
    WriteElem(xw, "title", title);
    WriteElem(xw, "link", link);
    WriteElem(xw, "description", desc);
    WriteElem(xw, "generator", generator);

    foreach (var item in items)
    {
        xw.WriteStartElement("item");
        WriteElem(xw, "title", item.Title);
        if (!string.IsNullOrWhiteSpace(item.Url)) WriteElem(xw, "link", item.Url!);
        WriteElem(xw, "guid", !string.IsNullOrWhiteSpace(item.Url) ? item.Url! : item.Id);
        WriteElem(xw, "pubDate", item.Timestamp.UtcDateTime.ToString("r"));
        if (!string.IsNullOrEmpty(item.Description))
        {
            xw.WriteStartElement("description");
            xw.WriteCData(item.Description);
            xw.WriteEndElement();
        }
        xw.WriteEndElement();
    }

    xw.WriteEndElement();
    xw.WriteEndElement();
    xw.WriteEndDocument();
    await xw.FlushAsync();
}

static void WriteElem(XmlWriter xw, string name, string value)
{
    xw.WriteStartElement(name);
    xw.WriteString(value);
    xw.WriteEndElement();
}

record FeedItem(string Id, string Title, string? Description, string? Url, DateTimeOffset Timestamp);

