using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml;

namespace ColorPickerApp;

/// <summary>
/// Локализация без сторонних библиотек: строки лежат в <see cref="Strings"/> парами (русский, английский).
/// Язык выбирается при запуске (настройка «Язык» или язык Windows) и меняется после перезапуска -
/// поэтому XAML может просто подставлять готовые строки, без привязок и уведомлений.
/// </summary>
public static class Loc
{
    public const string Russian = "ru";
    public const string English = "en";

    /// <summary>Текущий язык интерфейса: "ru" или "en".</summary>
    public static string Language { get; private set; } = Russian;

    public static bool IsEnglish => Language == English;

    /// <param name="setting">"ru", "en" или пусто - как в Windows.</param>
    public static void Init(string? setting)
    {
        Language = setting is Russian or English ? setting : DetectSystemLanguage();
    }

    /// <summary>Строка по ключу; если ключа нет - сам ключ (сразу видно, что забыли добавить).</summary>
    public static string T(string key)
    {
        if (!Strings.Table.TryGetValue(key, out var value))
            return key;
        return IsEnglish ? value.En : value.Ru;
    }

    /// <summary>Строка с подстановками {0}, {1}... (числа всегда через точку).</summary>
    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);

    // Из-за InvariantGlobalization CultureInfo.CurrentUICulture всегда Invariant - спрашиваем Windows напрямую
    private static string DetectSystemLanguage()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Russian;

            var primary = GetUserDefaultUILanguage() & 0x3FF;
            // Русский, украинский, белорусский, казахский - русский интерфейс, остальные - английский
            return primary is 0x19 or 0x22 or 0x23 or 0x3F ? Russian : English;
        }
        catch
        {
            return Russian;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}

/// <summary>
/// Перевод в XAML: <c>Text="{local:Tr settings_reset}"</c>.
/// Строка подставляется один раз при загрузке разметки.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
