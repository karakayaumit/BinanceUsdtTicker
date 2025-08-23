using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Nokta/virgül esnek ondalýk dönüþtürücü.
    /// Yazarken bozmamak için parse edilemeyen veya "tamamlanmamýþ"
    /// (1., 1,) girdilerde kaynaðý güncellemez (Binding.DoNothing).
    /// </summary>
    public class FlexibleDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                // Ekranda yerel ondalýk ayýracýyla göster
                var s = d.ToString("0.########", CultureInfo.InvariantCulture);
                var dec = culture.NumberFormat.NumberDecimalSeparator;
                return s.Replace(".", dec);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value?.ToString() ?? "").Trim();
            if (s.Length == 0) return Binding.DoNothing;

            // "tamamlanmamýþ" durumlar: sona . veya , eklenmiþse þimdi yazmayalým
            if (s.EndsWith(".") || s.EndsWith(",")) return Binding.DoNothing;

            // Hem . hem , varsa en sondaki ondalýktýr, diðeri binlik kabul edilir
            int iDot = s.LastIndexOf('.');
            int iCom = s.LastIndexOf(',');
            if (iDot >= 0 && iCom >= 0)
            {
                if (iCom > iDot) { s = s.Replace(".", ""); s = s.Replace(',', '.'); }
                else { s = s.Replace(",", ""); }
            }
            else if (iCom >= 0)
            {
                s = s.Replace(',', '.');
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;

            // Geçici olarak kabul etme; kullanýcý yazmaya devam edebilir
            return Binding.DoNothing;
        }
    }
}
