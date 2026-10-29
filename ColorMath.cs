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

    #region OKLCH (Oklab, Björn Ottosson)

    /// <summary>sRGB -> OKLCH. L: 0..1, C: 0..~0.37, H: 0..360°.</summary>
    public static (double L, double C, double H) ToOklch(Color color)
    {
        var r = SrgbToLinear(color.R / 255.0);
        var g = SrgbToLinear(color.G / 255.0);
        var b = SrgbToLinear(color.B / 255.0);

        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        var lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        var labA = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var labB = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        var chroma = Math.Sqrt(labA * labA + labB * labB);
        var hue = Math.Atan2(labB, labA) * 180 / Math.PI;
        if (hue < 0) hue += 360;
        if (chroma < 1e-4) { chroma = 0; hue = 0; } // серые: тон не определён
        return (lightness, chroma, hue);
    }

    /// <summary>
    /// OKLCH -> sRGB. Цвета вне sRGB приводятся к отображаемым уменьшением хромы
    /// (светлота и тон сохраняются) - так же рекомендует CSS Color 4.
    /// </summary>
    public static Color FromOklch(double lightness, double chroma, double hue, double alpha = 1.0)
    {
        lightness = Clamp01(lightness);
        chroma = Math.Max(0, chroma);

        var rgb = OklchToLinearSrgb(lightness, chroma, hue);
        if (!InGamut(rgb))
        {
            double lo = 0, hi = chroma;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) / 2;
                if (InGamut(OklchToLinearSrgb(lightness, mid, hue))) lo = mid; else hi = mid;
            }
            rgb = OklchToLinearSrgb(lightness, lo, hue);
        }

        return Color.FromArgb(
            (byte)Math.Round(Clamp01(alpha) * 255),
            (byte)Math.Round(Clamp01(LinearToSrgb(rgb.R)) * 255),
            (byte)Math.Round(Clamp01(LinearToSrgb(rgb.G)) * 255),
            (byte)Math.Round(Clamp01(LinearToSrgb(rgb.B)) * 255));
    }

    /// <summary>Помещается ли OKLCH-цвет в sRGB без изменения хромы.</summary>
    public static bool IsInSrgbGamut(double lightness, double chroma, double hue) =>
        InGamut(OklchToLinearSrgb(Clamp01(lightness), Math.Max(0, chroma), hue));

    /// <summary>
    /// Цвет для отрисовки шкал OKLCH: отображаемый цвет (хрома уменьшена при выходе за sRGB)
    /// и признак, был ли исходный цвет в гамме.
    /// </summary>
    public static Color OklchToDisplay(double lightness, double chroma, double hue, out bool inGamut)
    {
        inGamut = IsInSrgbGamut(lightness, chroma, hue);
        return FromOklch(lightness, chroma, hue);
    }

    private static (double R, double G, double B) OklchToLinearSrgb(double lightness, double chroma, double hue)
    {
        var rad = hue * Math.PI / 180;
        var labA = chroma * Math.Cos(rad);
        var labB = chroma * Math.Sin(rad);

        var l = lightness + 0.3963377774 * labA + 0.2158037573 * labB;
        var m = lightness - 0.1055613458 * labA - 0.0638541728 * labB;
        var s = lightness - 0.0894841775 * labA - 1.2914855480 * labB;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;

        return (
            +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    private static bool InGamut((double R, double G, double B) rgb)
    {
        const double eps = 1e-6;
        return rgb.R is >= -eps and <= 1 + eps
            && rgb.G is >= -eps and <= 1 + eps
            && rgb.B is >= -eps and <= 1 + eps;
    }

    private static double SrgbToLinear(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double LinearToSrgb(double c) =>
        c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    #endregion

    #region Контраст WCAG 2.x

    /// <summary>Относительная яркость по WCAG (0 - чёрный, 1 - белый). Альфа не учитывается.</summary>
    public static double RelativeLuminance(Color color) =>
        0.2126 * SrgbToLinear(color.R / 255.0)
        + 0.7152 * SrgbToLinear(color.G / 255.0)
        + 0.0722 * SrgbToLinear(color.B / 255.0);

    /// <summary>Полупрозрачный цвет поверх непрозрачного фона.</summary>
    public static Color Composite(Color foreground, Color background)
    {
        var a = foreground.A / 255.0;
        return Color.FromRgb(
            (byte)Math.Round(foreground.R * a + background.R * (1 - a)),
            (byte)Math.Round(foreground.G * a + background.G * (1 - a)),
            (byte)Math.Round(foreground.B * a + background.B * (1 - a)));
    }

    /// <summary>Контраст текста и фона: от 1 (одинаковые) до 21 (чёрный на белом).</summary>
    public static double ContrastRatio(Color foreground, Color background)
    {
        var bg = Color.FromRgb(background.R, background.G, background.B);
        var l1 = RelativeLuminance(Composite(foreground, bg));
        var l2 = RelativeLuminance(bg);
        var (lighter, darker) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (lighter + 0.05) / (darker + 0.05);
    }

    #endregion

    private static double Mod(double value, double mod)
    {
        var result = value % mod;
        if (result < 0)
            result += mod;
        return result;
    }
}
