using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ColorPickerApp
{
    public class ColorToBrushConverter : IValueConverter
    {
        private static Color GetContrastColor(Color c)
        {
            Color bg = Colors.White;

            double alpha = c.A / 255.0;

            double r = c.R * alpha + bg.R * (1 - alpha);
            double g = c.G * alpha + bg.G * (1 - alpha);
            double b = c.B * alpha + bg.B * (1 - alpha);

            // Яркость смешанного цвета (формула относительной яркости)
            double brightness = 0.299 * r + 0.587 * g + 0.114 * b;
            return brightness > 128 ? Colors.Black : Colors.White;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
                return new SolidColorBrush(GetContrastColor(color));

            if (value is string colorString && Color.TryParse(colorString, out var parsedColor))
                return new SolidColorBrush(GetContrastColor(parsedColor));

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
                return brush.Color;

            return Colors.Black;
        }
    }
}