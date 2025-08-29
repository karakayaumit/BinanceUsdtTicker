using System;

namespace BinanceUsdtTicker
{
    public class AlertHit
    {
        public DateTime TimestampUtc { get; set; }
        public string Symbol { get; set; } = "";
        public string TypeText { get; set; } = "";
        public string Message { get; set; } = "";

        // Log'u double tutmak istiyorsun; MainWindow'da (double) cast ile uyumlu
        public double Price { get; set; }

        // Uyarıyı (CS8603) engellemek için boş string fallback
        public string LocalTime =>
            TimestampUtc == default
                ? string.Empty
                : TimeZoneInfo.ConvertTimeFromUtc(TimestampUtc, TimeZoneInfo.Local).ToString("HH:mm:ss");

        public string BaseSymbol => Symbol.ToBaseSymbol();
    }
}
