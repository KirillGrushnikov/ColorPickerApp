using System;
using System.Globalization;
using System.Text;
using Avalonia.Media;

namespace ColorPickerApp;

/// <summary>
/// Шкала оттенков 50..950 от одного цвета (как палитры Tailwind / Material).
/// Строится в OKLCH: ступени ровные по светлоте, тон не «уплывает» при осветлении и затемнении.
/// </summary>
public static class ShadeScale
{
    public static readonly int[] Steps = { 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950 };

    // Светлота ступеней и форма кривой хромы - по образцу палитры Tailwind 4 (oklch)
    private static readonly double[] TargetLightness = { 0.971, 0.936, 0.885, 0.808, 0.704, 0.637, 0.577, 0.505, 0.444, 0.396, 0.258 };
    private static readonly double[] ChromaCurve = { 0.055, 0.135, 0.26, 0.48, 0.81, 1.0, 1.03, 0.90, 0.75, 0.59, 0.39 };

    /// <summary>
    /// Цвета шкалы и индекс ступени, на которую попал исходный цвет (он остаётся в шкале без изменений).
    /// </summary>
    public static (Color[] Colors, int BaseIndex) Generate(Color baseColor)
    {
        var opaque = Color.FromRgb(baseColor.R, baseColor.G, baseColor.B);
        var (l, c, h) = ColorMath.ToOklch(opaque);

        // Ступень, ближайшая по светлоте к исходному цвету
        var baseIndex = 0;
        for (var i = 1; i < Steps.Length; i++)
        {
            if (Math.Abs(TargetLightness[i] - l) < Math.Abs(TargetLightness[baseIndex] - l))
                baseIndex = i;
        }

        // Сдвиг кривой светлоты: на исходной ступени - ровно цвет пользователя, к краям сдвиг сходит на нет
        var delta = l - TargetLightness[baseIndex];
        var last = Steps.Length - 1;
        var colors = new Color[Steps.Length];
        for (var i = 0; i < Steps.Length; i++)
        {
            if (i == baseIndex)
            {
                colors[i] = opaque;
                continue;
            }

            double weight = i < baseIndex
                ? (double)i / baseIndex
                : (double)(last - i) / (last - baseIndex);

            var lightness = Math.Clamp(TargetLightness[i] + delta * weight, 0, 1);
            // Хрома растёт к середине шкалы, но не более чем втрое: пастельный цвет даёт спокойную шкалу, а не кричащую
            var chromaRatio = Math.Min(ChromaCurve[i] / ChromaCurve[baseIndex], 3.0);
            var chroma = Math.Min(c * chromaRatio, 0.37);
            colors[i] = ColorMath.FromOklch(lightness, chroma, h);
        }

        return (colors, baseIndex);
    }

    /// <summary>
    /// Текст для вставки в код. exportIndex: 0 - CSS-переменные, 1 - Tailwind 4 (@theme), 2 - Tailwind 3 / JS-объект, 3 - список.
    /// valueIndex: 0 - HEX, 1 - OKLCH, 2 - RGB.
    /// </summary>
    public static string Export(Color[] colors, string? name, int exportIndex, int valueIndex)
    {
        var id = SanitizeName(name);
        string Value(Color c) => valueIndex switch
        {
            1 => ColorFormat.Oklch(c),
            2 => ColorFormat.Rgb(c),
            _ => ColorFormat.Hex(c),
        };

        var sb = new StringBuilder();
        switch (exportIndex)
        {
            case 1:
                sb.AppendLine("@theme {");
                for (var i = 0; i < colors.Length; i++)
                    sb.Append("  --color-").Append(id).Append('-').Append(Steps[i]).Append(": ").Append(Value(colors[i])).AppendLine(";");
                sb.AppendLine("}");
                break;
            case 2:
                sb.Append(QuoteIfNeeded(id)).AppendLine(": {");
                for (var i = 0; i < colors.Length; i++)
                    sb.Append("  ").Append(Steps[i]).Append(": '").Append(Value(colors[i])).AppendLine("',");
                sb.AppendLine("},");
                break;
            case 3:
                for (var i = 0; i < colors.Length; i++)
                    sb.Append(Steps[i]).Append(": ").AppendLine(Value(colors[i]));
                break;
            default:
                sb.AppendLine(":root {");
                for (var i = 0; i < colors.Length; i++)
                    sb.Append("  --").Append(id).Append('-').Append(Steps[i]).Append(": ").Append(Value(colors[i])).AppendLine(";");
                sb.AppendLine("}");
                break;
        }

        return sb.ToString();
    }

    /// <summary>"Brand Blue" -> "brand-blue"; пусто -> "primary".</summary>
    public static string SanitizeName(string? name)
    {
        var sb = new StringBuilder();
        foreach (var ch in (name ?? "").Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) && sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        var result = sb.ToString().Trim('-');
        return result.Length == 0 ? "primary" : result;
    }

    private static string QuoteIfNeeded(string id) =>
        id.Contains('-') || char.IsDigit(id[0]) ? $"'{id}'" : id;
}

/// <summary>Подбор ближайшего цвета, проходящего нужный контраст WCAG.</summary>
public static class ContrastFix
{
    /// <summary>
    /// Меняет только светлоту OKLCH (тон и хрома сохраняются, хрома лишь уменьшается, если не помещается в sRGB).
    /// Ищет в обе стороны - светлее и темнее - и берёт вариант с наименьшим изменением.
    /// Если цель недостижима, возвращает цвет с максимально возможным контрастом и Reached = false.
    /// </summary>
    public static (Color Color, bool Reached, double Ratio) Find(Color start, Func<Color, double> ratioOf, double target)
    {
        var opaque = Color.FromRgb(start.R, start.G, start.B);
        var (l0, c, h) = ColorMath.ToOklch(opaque);

        Color? best = null;
        var bestDistance = double.MaxValue;
        Color fallback = opaque;
        var fallbackRatio = ratioOf(opaque);

        foreach (var end in new[] { 1.0, 0.0 })
        {
            var endColor = ColorMath.FromOklch(end, c, h);
            var endRatio = ratioOf(endColor);
            if (endRatio > fallbackRatio)
            {
                fallback = endColor;
                fallbackRatio = endRatio;
            }

            if (endRatio < target)
                continue; // в эту сторону цель не достигается

            // Граница «не проходит -> проходит» между исходной светлотой и краем
            double fail = l0, pass = end;
            for (var i = 0; i < 32; i++)
            {
                var mid = (fail + pass) / 2;
                if (ratioOf(ColorMath.FromOklch(mid, c, h)) >= target) pass = mid; else fail = mid;
            }

            // Округление до 8 бит может уронить контраст на сотые - чуть дожимаем
            var step = end > l0 ? 0.002 : -0.002;
            var color = ColorMath.FromOklch(pass, c, h);
            for (var i = 0; i < 20 && ratioOf(color) < target; i++)
            {
                pass = Math.Clamp(pass + step, 0, 1);
                color = ColorMath.FromOklch(pass, c, h);
            }
            if (ratioOf(color) < target)
                continue;

            var distance = Math.Abs(pass - l0);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = color;
            }
        }

        return best is Color found
            ? (found, true, ratioOf(found))
            : (fallback, false, fallbackRatio);
    }
}

public static class ColorFormat
{
    /// <summary>CSS-запись oklch(L% C H) для непрозрачного цвета.</summary>
    public static string Oklch(Color color)
    {
        var (l, c, h) = ColorMath.ToOklch(Color.FromRgb(color.R, color.G, color.B));
        return string.Format(CultureInfo.InvariantCulture, "oklch({0:0.0}% {1:0.000} {2:0.0})", l * 100, c, h);
    }

    public static string Rgb(Color color) =>
        string.Format(CultureInfo.InvariantCulture, "rgb({0}, {1}, {2})", color.R, color.G, color.B);

    /// <summary>#rrggbb для показа и копирования - регистр по настройке «Заглавные буквы».</summary>
    public static string Hex(Color color) => HexCase(ColorMath.ToHex(color));

    /// <summary>Применяет настройку регистра HEX к уже готовой строке.</summary>
    public static string HexCase(string hex) => App.Settings.HexUppercase ? hex.ToUpperInvariant() : hex;
}
