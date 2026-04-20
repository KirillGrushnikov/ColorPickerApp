using System;
using Avalonia.Media;

namespace ColorPickerApp;

public static class ColorMath
{
    public static Color FromHsv(double h, double s, double v, double a = 1.0)
    {
        h = Mod(h, 360.0);
        s = Clamp01(s);
        v = Clamp01(v);
        a = Clamp01(a);

        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = v - c;

        (double r, double g, double b) rgb = h switch
        {
            >= 0 and < 60 => (c, x, 0),
            >= 60 and < 120 => (x, c, 0),
            >= 120 and < 180 => (0, c, x),
            >= 180 and < 240 => (0, x, c),
            >= 240 and < 300 => (x, 0, c),
            _ => (c, 0, x),
        };

        var r = (byte)Math.Round((rgb.r + m) * 255);
        var g = (byte)Math.Round((rgb.g + m) * 255);
        var b = (byte)Math.Round((rgb.b + m) * 255);
        var alpha = (byte)Math.Round(a * 255);
        return Color.FromArgb(alpha, r, g, b);
    }

    public static (double H, double S, double V) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var h = 0.0;
        if (delta > 0)
        {
            if (Math.Abs(max - r) < double.Epsilon)
                h = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < double.Epsilon)
                h = 60 * (((b - r) / delta) + 2);
            else
                h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0)
            h += 360;

        var s = max <= 0 ? 0 : delta / max;
        var v = max;
        return (h, s, v);
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:x2}{color.G:x2}{color.B:x2}";
    }

    public static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    private static double Mod(double value, double mod)
    {
        var result = value % mod;
        if (result < 0)
            result += mod;
        return result;
    }
}
