using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ColorPickerApp
{
    /// <summary>true -> "✓ {параметр}", false -> "✗ {параметр}" (значки WCAG AA/AAA).</summary>
    public class PassMarkConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var mark = value is true ? "✓" : "✗";
            return parameter is string text ? $"{mark} {text}" : mark;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
