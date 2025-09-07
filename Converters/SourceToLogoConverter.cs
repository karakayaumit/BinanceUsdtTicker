using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BinanceUsdtTicker
{
    public class SourceToLogoConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string src)
                return null;

            src = src.ToLowerInvariant();
            return src switch
            {
                "bybit" => CreateLogo("BYBIT", Color.FromRgb(0xF7, 0x93, 0x1A), Colors.White),
                "kucoin" => CreateLogo("KUCOIN", Color.FromRgb(0x28, 0xD1, 0xA7), Colors.White),
                "okx" => CreateLogo("OKX", Colors.Black, Colors.White),
                _ => null,
            };
        }

        private static ImageSource CreateLogo(string text, Color background, Color foreground)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16,
                new SolidColorBrush(foreground),
                1.0);

            var width = Math.Ceiling(ft.Width) + 4;
            var height = Math.Ceiling(ft.Height) + 4;

            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                ctx.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));
                ctx.DrawText(ft, new System.Windows.Point(2, 2));
            }

            var bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            return bmp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
