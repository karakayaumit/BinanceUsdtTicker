using System;
using System.Collections.Generic;

namespace BinanceUsdtTicker
{
    public class NewsItem : EventArgs
    {
        public NewsItem(string id, string source, DateTime timestamp, string title, string? titleTranslate, string? body, string link, NewsType type, IReadOnlyList<string> symbols)
        {
            Id = id;
            Source = source;
            Timestamp = timestamp;
            Title = title;
            TitleTranslate = titleTranslate;
            Body = body;
            Link = link;
            Type = type;
            Symbols = symbols;
        }

        public string Id { get; }
        public string Source { get; }
        public DateTime Timestamp { get; }
        public string Title { get; }
        public string? TitleTranslate { get; }
        public string? Body { get; }
        public string Link { get; }
        public NewsType Type { get; }
        public IReadOnlyList<string> Symbols { get; }
        public string SymbolsDisplay => string.Join(", ", Symbols);
    }
}
