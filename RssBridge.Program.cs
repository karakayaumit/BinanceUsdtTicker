#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Bind to a fixed port
builder.WebHost.UseUrls("http://localhost:5000");

var app = builder.Build();

var http = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
})
{
    Timeout = TimeSpan.FromSeconds(10)
};
http.DefaultRequestHeaders.UserAgent.ParseAdd("RssBridge/1.0 (+localhost)");

app.MapGet("/", () => Results.Text(
    "RssBridge running.\n" +
    "GET /rss/bybit-new?locale=en-US&limit=20\n" +
    "GET /rss/kucoin-new?lang=en_US&pageSize=20&page=1\n",
    "text/plain"));

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.MapGet("/rss/{feed}", async (string feed, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    var q = ctx.Request.Query;

    try
    {
        switch (feed)
        {
            case "bybit-new":
            {
                var locale = q["locale"].FirstOrDefault() ?? "en-US";
                var limit = int.TryParse(q["limit"].FirstOrDefault(), out var l) ? Math.Clamp(l,1,50) : 20;
                var url = $"https://api.bybit.com/v5/announcements/index?locale={Uri.EscapeDataString(locale)}&type=new_crypto&limit={limit}";
                app.Logger.LogInformation("Upstream GET {Url}", url);
                using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                if (!r.IsSuccessStatusCode)
                    return Results.Problem(title: "Bybit upstream error", detail: r.StatusCode.ToString(), statusCode: 502);

                using var s = await r.Content.ReadAsStreamAsync(ctx.RequestAborted);
                using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ctx.RequestAborted);
                var root = doc.RootElement;
                if (root.TryGetProperty("result", out var result) && result.TryGetProperty("list", out var list))
                {
                    ctx.Response.ContentType = "application/rss+xml; charset=utf-8";
                    await WriteRss(ctx.Response.Body, "Bybit – New Crypto", "https://announcements.bybit.com/en/?category=new_crypto&page=1", list, MapBybit);
                    return Results.Empty;
                }
                return Results.Problem(title: "Bybit JSON unexpected", statusCode: 502);
            }

            case "kucoin-new":
            {
                var lang = q["lang"].FirstOrDefault() ?? "en_US";
                var pageSize = int.TryParse(q["pageSize"].FirstOrDefault(), out var ps) ? Math.Clamp(ps,1,50) : 20;
                var page = int.TryParse(q["page"].FirstOrDefault(), out var pg) ? Math.Max(pg,1) : 1;
                var url = $"https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang={Uri.EscapeDataString(lang)}&pageSize={pageSize}&currentPage={page}";
                app.Logger.LogInformation("Upstream GET {Url}", url);
                using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                if (!r.IsSuccessStatusCode)
                    return Results.Problem(title: "KuCoin upstream error", detail: r.StatusCode.ToString(), statusCode: 502);

                using var s = await r.Content.ReadAsStreamAsync(ctx.RequestAborted);
                using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ctx.RequestAborted);
                var data = doc.RootElement.GetProperty("data");
                var items = data.GetProperty("items");

                ctx.Response.ContentType = "application/rss+xml; charset=utf-8";
                await WriteRss(ctx.Response.Body, "KuCoin – New Listings", "https://www.kucoin.com/announcement/new-listings", items, MapKucoin);
                return Results.Empty;
            }
        }

        return Results.NotFound(new { error = "unknown feed" });
    }
    catch (TaskCanceledException)
    {
        return Results.Problem(title: "Request canceled/timeout", statusCode: 504);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "RSS handler failed");
        return Results.Problem(title: "Server error", detail: ex.Message, statusCode: 500);
    }
});

app.Run();

static async Task WriteRss(Stream output, string channelTitle, string channelLink, JsonElement items, Func<JsonElement, FeedItem> map)
{
    var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
    using var xw = XmlWriter.Create(output, settings);

    xw.WriteStartDocument();
    xw.WriteStartElement("rss");
    xw.WriteAttributeString("version", "2.0");
    xw.WriteStartElement("channel");
    WriteElem(xw, "title", channelTitle);
    WriteElem(xw, "link", channelLink);
    WriteElem(xw, "description", channelTitle);

    foreach (var el in items.EnumerateArray())
    {
        var item = map(el);
        xw.WriteStartElement("item");
        WriteElem(xw, "title", item.Title);
        if (!string.IsNullOrWhiteSpace(item.Url)) WriteElem(xw, "link", item.Url!);
        WriteElem(xw, "guid", !string.IsNullOrWhiteSpace(item.Url) ? item.Url! : item.Id);
        WriteElem(xw, "pubDate", item.Timestamp.UtcDateTime.ToString("r"));
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            xw.WriteStartElement("description");
            xw.WriteCData(item.Description!);
            xw.WriteEndElement();
        }
        xw.WriteEndElement();
    }

    xw.WriteEndElement();
    xw.WriteEndElement();
    xw.WriteEndDocument();
    await xw.FlushAsync();
}

static FeedItem MapBybit(JsonElement it)
{
    var id = it.TryGetProperty("id", out var pId) ? pId.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
    var title = it.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
    var desc = it.TryGetProperty("content", out var pDesc) ? pDesc.GetString() : null;
    var url = it.TryGetProperty("url", out var pUrl) ? pUrl.GetString() : null;
    var ts = it.TryGetProperty("dateTimestamp", out var pTs) && long.TryParse(pTs.GetString(), out var t)
        ? DateTimeOffset.FromUnixTimeMilliseconds(t)
        : DateTimeOffset.UtcNow;
    return new FeedItem(id, title, desc, url, ts);
}

static FeedItem MapKucoin(JsonElement it)
{
    var id = it.TryGetProperty("annId", out var pId) ? pId.GetInt64().ToString() : Guid.NewGuid().ToString("n");
    var title = it.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
    var desc = it.TryGetProperty("annDesc", out var pDesc) ? pDesc.GetString() : null;
    var url = it.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
    var ts = it.TryGetProperty("cTime", out var pTime)
        ? DateTimeOffset.FromUnixTimeMilliseconds(pTime.GetInt64())
        : DateTimeOffset.UtcNow;
    return new FeedItem(id, title, desc, url, ts);
}

static void WriteElem(XmlWriter xw, string name, string value)
{
    xw.WriteStartElement(name);
    xw.WriteString(value);
    xw.WriteEndElement();
}

record FeedItem(string Id, string Title, string? Description, string? Url, DateTimeOffset Timestamp);

