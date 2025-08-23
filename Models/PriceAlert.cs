using System;
using System.Text.Json.Serialization;

namespace BinanceUsdtTicker
{
    public enum AlertType
    {
        PriceAtOrAbove,            // Fiyat >= A
        PriceAtOrBelow,            // Fiyat <= A
        PriceBetween,              // A <= Fiyat <= B
        ChangeSinceStartAtOrAbove, // Başlangıca göre % >= A
        ChangeSinceStartAtOrBelow  // Başlangıca göre % <= A
    }

    public class PriceAlert
    {
        public string Symbol { get; set; } = string.Empty;
        public AlertType Type { get; set; }

        // DOUBLE -> DECIMAL
        public decimal A { get; set; }
        public decimal? B { get; set; }

        public bool OneShot { get; set; } = true;
        public int CooldownSeconds { get; set; } = 0;
        public bool Enabled { get; set; } = true;

        // Çalışma zamanı alanlar: JSON'a yazılmasın
        [JsonIgnore] private bool _wasInCondition = false;
        [JsonIgnore] private DateTime? _lastTriggeredUtc;

        public bool Evaluate(TickerRow row, DateTime utcNow, out string message)
        {
            message = string.Empty;
            if (!Enabled) return false;
            if (!row.Symbol.Equals(Symbol, StringComparison.OrdinalIgnoreCase)) return false;

            // PriceBetween için alt/üst bandı güvenli al
            decimal lower = B.HasValue ? Math.Min(A, B.Value) : A;
            decimal upper = B.HasValue ? Math.Max(A, B.Value) : A;

            bool cond = Type switch
            {
                AlertType.PriceAtOrAbove => row.Price >= A,
                AlertType.PriceAtOrBelow => row.Price <= A,
                AlertType.PriceBetween => row.Price >= lower && row.Price <= upper,
                AlertType.ChangeSinceStartAtOrAbove => row.ChangeSinceStartPercent >= A,
                AlertType.ChangeSinceStartAtOrBelow => row.ChangeSinceStartPercent <= A,
                _ => false
            };

            // sadece "duruma giriş"te tetikle
            bool entering = !_wasInCondition && cond;

            // cooldown kontrolü
            bool cooldownOk = _lastTriggeredUtc is null
                              || CooldownSeconds <= 0
                              || utcNow - _lastTriggeredUtc.Value >= TimeSpan.FromSeconds(CooldownSeconds);

            if (entering && cooldownOk)
            {
                _lastTriggeredUtc = utcNow;
                if (OneShot) Enabled = false;

                message = Type switch
                {
                    AlertType.PriceAtOrAbove =>
                        $"{Symbol}: Fiyat {row.Price:N6} → ≥ {A:N6}",
                    AlertType.PriceAtOrBelow =>
                        $"{Symbol}: Fiyat {row.Price:N6} → ≤ {A:N6}",
                    AlertType.PriceBetween =>
                        $"{Symbol}: Fiyat {row.Price:N6} → [{lower:N6}, {upper:N6}] bandına GİRDİ",
                    AlertType.ChangeSinceStartAtOrAbove =>
                        $"{Symbol}: Başlangıca göre %{row.ChangeSinceStartPercent:N2} → ≥ %{A:N2}",
                    AlertType.ChangeSinceStartAtOrBelow =>
                        $"{Symbol}: Başlangıca göre %{row.ChangeSinceStartPercent:N2} → ≤ %{A:N2}",
                    _ => $"{Symbol}: Alarm"
                };
            }

            _wasInCondition = cond;
            return !string.IsNullOrEmpty(message);
        }
    }
}
