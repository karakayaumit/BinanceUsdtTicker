#nullable enable
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


var http = new HttpClient(new HttpClientHandler{ AutomaticDecompression = System.Net.DecompressionMethods.All })
{
    Timeout = TimeSpan.FromSeconds(10)
};
http.DefaultRequestHeaders.UserAgent.ParseAdd("KucoinNewListingsRSS/1.0 (+https://kucoin.com)");


app.MapGet("/rss", async (HttpContext ctx) =>
{
    var lang = ctx.Request.Query["lang"].FirstOrDefault() ?? "en_US"; // tr_TR destekli
    var pageSize = int.TryParse(ctx.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? Math.Clamp(ps, 1, 50) : 20;
    var page = int.TryParse(ctx.Request.Query["page"].FirstOrDefault(), out var pg) ? Math.Max(pg, 1) : 1;


    var url = $"https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang={Uri.EscapeDataString(lang)}&pageSize={pageSize}&currentPage={page}";
    using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
    resp.EnsureSuccessStatusCode();


    using var stream = await resp.Content.ReadAsStreamAsync(ctx.RequestAborted);
    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.RequestAborted);


    // JSON şeması (özet): { code, data: { totalNum, items: [ { annId, annTitle, annType[], annDesc, cTime(ms), language, annUrl } ], ... } }
    var root = doc.RootElement;
    var data = root.GetProperty("data");
    var items = data.GetProperty("items");


    ctx.Response.ContentType = "application/rss+xml; charset=utf-8";


    var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
    using var xw = XmlWriter.Create(ctx.Response.Body, settings);


    xw.WriteStartDocument();
    xw.WriteStartElement("rss");
    xw.WriteAttributeString("version", "2.0");
    xw.WriteStartElement("channel");
    WriteElem(xw, "title", "KuCoin – New Listings (Unofficial RSS)");
    WriteElem(xw, "link", "https://www.kucoin.com/announcement/new-listings");
    WriteElem(xw, "description", $"New Listings announcements via KuCoin API (lang={lang})");
    WriteElem(xw, "generator", "KucoinNewListingsRSS/1.0");


    foreach (var it in items.EnumerateArray())
    {
        var id = it.TryGetProperty("annId", out var pId) ? pId.GetInt64().ToString() : Guid.NewGuid().ToString("n");
        var title = it.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
        var desc = it.TryGetProperty("annDesc", out var pDesc) ? pDesc.GetString() : null;
        var urlItem = it.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
        var cTimeMs = it.TryGetProperty("cTime", out var pTime) ? pTime.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pubDate = DateTimeOffset.FromUnixTimeMilliseconds(cTimeMs).UtcDateTime.ToString("r"); // RFC1123


        xw.WriteStartElement("item");
        WriteElem(xw, "title", title);
        if (!string.IsNullOrWhiteSpace(urlItem)) WriteElem(xw, "link", urlItem!);
        WriteElem(xw, "guid", !string.IsNullOrWhiteSpace(urlItem) ? urlItem! : id);
        WriteElem(xw, "pubDate", pubDate);
        if (!string.IsNullOrEmpty(desc))
        {
            xw.WriteStartElement("description");
            xw.WriteCData(desc);
            xw.WriteEndElement();
        }
        xw.WriteEndElement();
    }


    xw.WriteEndElement(); // channel
    xw.WriteEndElement(); // rss
    xw.WriteEndDocument();
});


app.Run();


static void WriteElem(XmlWriter xw, string name, string value)
{
    xw.WriteStartElement(name);
    xw.WriteString(value);
    xw.WriteEndElement();
}

