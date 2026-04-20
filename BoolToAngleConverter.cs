using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ColorPickerApp
{
    internal class BoolToAngleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? 180.0 : 0.0;
            }

            throw new NotImplementedException();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
