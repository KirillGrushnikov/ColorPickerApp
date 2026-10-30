using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ColorPickerApp.Services;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ColorPickerApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // Аргументы событий создаются один раз: RaiseAll вызывается на каждое движение мыши по кругу
    private static readonly PropertyChangedEventArgs[] AllColorProperties =
    {
        new(nameof(Hue)),
        new(nameof(Saturation)),
        new(nameof(Value)),
        new(nameof(Alpha)),
        new(nameof(R)),
        new(nameof(G)),
        new(nameof(B)),
        new(nameof(NormalR)),
        new(nameof(NormalG)),
        new(nameof(NormalB)),
        new(nameof(Hex)),
        new(nameof(RGB565)),
        new(nameof(Oklch)),
        new(nameof(OklchL)),
        new(nameof(OklchC)),
        new(nameof(OklchH)),
        new(nameof(OklchOutOfGamut)),
        new(nameof(OpaqueColor)),
        new(nameof(SelectedBrush)),
        new(nameof(ContrastRatioText)),
        new(nameof(PassAaNormal)),
        new(nameof(PassAaaNormal)),
        new(nameof(PassAaLarge)),
        new(nameof(PassAaaLarge)),
        new(nameof(PassUi)),
        // SelectedColor последним: на него подписано окно (текст копирования, гармонии)
        new(nameof(SelectedColor)),
    };

    private bool _internalUpdate;
    private double _hue = 0;
    private double _saturation = 1.0;
    private double _value = 1.0;
    private double _alpha = 1.0;
    private int _r;
    private int _g;
    private int _b;
    private int _rgb565 = 0;
    private string _hex = "#000000";
    private string _copyFormat = "rgb";
    private int _copyFormatIndex;
    private bool _copyWithAlpha;
    private KeyGesture? _pickerKeyGesture;
    private KeyGesture? _openWindowKeyGesture;
    private Color _contrastBackground;

    // OKLCH-«намерение»: то, что выставил пользователь ползунками/кругом. Хранится отдельно от RGB,
    // чтобы ползунок хромы не прыгал при выходе за sRGB, а тон не терялся у серых цветов.
    private double _oklchL = 62.8;
    private double _oklchC;
    private double _oklchH;
    private bool _oklchDriving;

    public MainWindowViewModel()
    {
        // Поля, а не свойства: иначе каждое присваивание в конструкторе писало бы settings.json
        _copyFormatIndex = App.Settings.CopyFormatIndex;
        _copyWithAlpha = App.Settings.IsCopyWithAlpha;
        _contrastBackground = Color.TryParse(App.Settings.ContrastBackground, out var bg)
            ? Color.FromRgb(bg.R, bg.G, bg.B)
            : Colors.White;
        SetFromHsv(_hue, _saturation, _value, _alpha);

        // Открываемся с последним цветом прошлого сеанса
        if (App.Settings.RememberLastColor && Color.TryParse(App.Settings.LastColor, out var last))
            SetColor(last);

        App.Settings.PropertyChanged += Settings_PropertyChanged;
    }

    /// <summary>Подсказка иконки в трее: предупреждает, если хоткей не удалось зарегистрировать.</summary>
    public string TrayToolTip
    {
        get
        {
            var hotkeys = App.Hotkeys;
            if (hotkeys is null)
                return "Color Picker Pro";

            var text = "Color Picker Pro";
            if (hotkeys.PickerStatus is HotkeyStatus.Busy or HotkeyStatus.Invalid or HotkeyStatus.Duplicate or HotkeyStatus.HookFailed)
                text += "\n" + Loc.F("tray_picker_fail", App.Settings.PickerHotkey);
            if (hotkeys.OpenWindowStatus is HotkeyStatus.Busy or HotkeyStatus.Invalid or HotkeyStatus.Duplicate or HotkeyStatus.HookFailed)
                text += "\n" + Loc.F("tray_open_fail", App.Settings.OpenWindowHotkey);
            return text;
        }
    }

    public void RefreshHotkeyStatus() => RaisePropertyChanged(nameof(TrayToolTip));

    public KeyGesture? PickerKeyGesture => _pickerKeyGesture ??= TryParseGesture(App.Settings.PickerHotkey);

    public KeyGesture? OpenWindowKeyGesture => _openWindowKeyGesture ??= TryParseGesture(App.Settings.OpenWindowHotkey);

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsStore.PickerHotkey))
        {
            _pickerKeyGesture = null;
            RaisePropertyChanged(nameof(PickerKeyGesture));
        }
        else if (e.PropertyName == nameof(AppSettingsStore.OpenWindowHotkey))
        {
            _openWindowKeyGesture = null;
            RaisePropertyChanged(nameof(OpenWindowKeyGesture));
        }
        else if (e.PropertyName is nameof(AppSettingsStore.HexAlphaFirst) or nameof(AppSettingsStore.HexUppercase))
        {
            // Поменялся порядок альфы в HEX - обновляем поле Hex и текст для копирования
            _internalUpdate = true;
            try
            {
                UpdateDerived();
                RaiseAll();
                RaisePropertyChanged(nameof(ContrastBackgroundHex));
            }
            finally
            {
                _internalUpdate = false;
            }
        }
    }

    /// <summary>
    /// HEX с учётом настройки порядка альфы: #RRGGBBAA (CSS) или #AARRGGBB (Android/XAML).
    /// Без альфы - всегда #RRGGBB.
    /// </summary>
    public static string FormatHex(byte alpha, int r, int g, int b, bool withAlpha)
    {
        var inv = CultureInfo.InvariantCulture;
        var hex = !withAlpha
            ? string.Format(inv, "#{0:x2}{1:x2}{2:x2}", r, g, b)
            : App.Settings.HexAlphaFirst
                ? string.Format(inv, "#{0:x2}{1:x2}{2:x2}{3:x2}", alpha, r, g, b)
                : string.Format(inv, "#{0:x2}{1:x2}{2:x2}{3:x2}", r, g, b, alpha);
        return ColorFormat.HexCase(hex);
    }

    private byte AlphaByte => (byte)Math.Round(_alpha * 255);

    private static KeyGesture? TryParseGesture(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            return KeyGesture.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    public double Hue
    {
        get => _hue;
        set
        {
            this.RaiseAndSetIfChanged(ref _hue, WrapHue(value));
            UpdateFromHsv();
        }
    }

    public double Saturation
    {
        get => _saturation;
        set
        {
            this.RaiseAndSetIfChanged(ref _saturation, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            this.RaiseAndSetIfChanged(ref _alpha, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public int R
    {
        get => _r;
        set
        {
            this.RaiseAndSetIfChanged(ref _r, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public int G
    {
        get => _g;
        set
        {
            this.RaiseAndSetIfChanged(ref _g, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public int B
    {
        get => _b;
        set
        {
            this.RaiseAndSetIfChanged(ref _b, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public double NormalR
    {
        get => _r / 255.0;
        set
        {
            R = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public double NormalG
    {
        get => _g / 255.0;
        set
        {
            G = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public double NormalB
    {
        get => _b / 255.0;
        set
        {
            B = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public string Hex
    {
        get => _hex;
        set
        {
            this.RaiseAndSetIfChanged(ref _hex, value);
            TryUpdateFromHex(value);
        }
    }

    public int RGB565
    {
        get => _rgb565;
        set
        {
            value = Math.Clamp(value, 0, 0xFFFF);
            if (!this.RaiseAndSetIfChanged(ref _rgb565, value) || _internalUpdate)
                return;

            int r5 = (value >> 11) & 0x1F;
            int g6 = (value >> 5) & 0x3F;
            int b5 = value & 0x1F;

            // Раньше R, G, B выставлялись по очереди - три полных пересчёта и три RaiseAll
            _r = (r5 << 3) | (r5 >> 2);
            _g = (g6 << 2) | (g6 >> 4);
            _b = (b5 << 3) | (b5 >> 2);
            UpdateFromRgb();
        }
    }

    #region OKLCH

    /// <summary>Текущий цвет в CSS-записи oklch(L% C H), без альфы.</summary>
    public string Oklch => FormatOklch(includeAlpha: false);

    /// <summary>Светлота 0..100 %.</summary>
    public double OklchL
    {
        get => _oklchL;
        set
        {
            if (!IsDisplayEcho(value, _oklchL, 1))
                SetOklch(value, _oklchC, _oklchH);
        }
    }

    /// <summary>Хрома 0..0.4.</summary>
    public double OklchC
    {
        get => _oklchC;
        set
        {
            if (!IsDisplayEcho(value, _oklchC, 3))
                SetOklch(_oklchL, value, _oklchH);
        }
    }

    /// <summary>Тон 0..360°.</summary>
    public double OklchH
    {
        get => _oklchH;
        set
        {
            if (!IsDisplayEcho(value, _oklchH, 1))
                SetOklch(_oklchL, _oklchC, value);
        }
    }

    /// <summary>Выбранный OKLCH не помещается в sRGB - показан ближайший отображаемый цвет.</summary>
    public bool OklchOutOfGamut => !ColorMath.IsInSrgbGamut(_oklchL / 100, _oklchC, _oklchH);

    /// <summary>
    /// Поле NumericUpDown возвращает значение, округлённое до своего формата (F1/F3), например при потере
    /// фокуса. Это не правка пользователя - иначе цвет незаметно сдвигался бы на округление.
    /// </summary>
    private static bool IsDisplayEcho(double value, double current, int decimals) =>
        value != current && Math.Abs(value - Math.Round(current, decimals)) < 1e-9;

    private void SetOklch(double l, double c, double h, bool force = false)
    {
        // Пока ViewModel рассылает уведомления об уже изменённом цвете, контролы OKLCH (поля NumericUpDown
        // даже на неоткрытой вкладке) могут «вернуть» значение обратно - округлённое при переводе double -> decimal.
        // Раньше такой возврат пересчитывал цвет из OKLCH, и тон основного круга прыгал (у серых - на красный).
        if (_internalUpdate)
            return;

        l = Math.Clamp(l, 0, 100);
        c = Math.Clamp(c, 0, 0.4);
        h %= 360;
        if (h < 0) h += 360;

        // Отличия на уровне шума округления - не изменение
        if (!force
            && Math.Abs(l - _oklchL) < 1e-6
            && Math.Abs(c - _oklchC) < 1e-7
            && Math.Abs(h - _oklchH) < 1e-6)
            return;

        _oklchL = l;
        _oklchC = c;
        _oklchH = h;

        _oklchDriving = true;
        try
        {
            SetColor(ColorMath.FromOklch(l / 100, c, h, _alpha));
        }
        finally
        {
            _oklchDriving = false;
        }
    }

    // При изменении цвета любым другим способом (круг HSV, RGB, пипетка...) пересчитываем OKLCH из RGB
    private void SyncOklchFromRgb()
    {
        if (_oklchDriving)
            return;

        var (l, c, h) = ColorMath.ToOklch(Color.FromRgb((byte)_r, (byte)_g, (byte)_b));
        _oklchL = l * 100;
        _oklchC = c;
        if (c > 0)
            _oklchH = h; // у серых тон не определён - оставляем прежний
    }

    private string FormatOklch(bool includeAlpha)
    {
        // В гамме - ровно то, что выставлено ползунками; вне гаммы - фактически показанный цвет
        double l, c, h;
        if (OklchOutOfGamut)
        {
            (l, c, h) = ColorMath.ToOklch(OpaqueColor);
        }
        else
        {
            l = _oklchL / 100;
            c = _oklchC;
            h = _oklchH;
        }

        var inv = CultureInfo.InvariantCulture;
        return includeAlpha
            ? string.Format(inv, "oklch({0:0.0}% {1:0.000} {2:0.0} / {3:0.00})", l * 100, c, h, Alpha)
            : string.Format(inv, "oklch({0:0.0}% {1:0.000} {2:0.0})", l * 100, c, h);
    }

    /// <summary>
    /// Принимает "oklch(62.8% 0.258 29.2)", "oklch(0.628 0.258 29.2 / 0.5)" или просто "62.8 0.258 29.2".
    /// Возвращает false, если строку не удалось разобрать.
    /// </summary>
    public bool TrySetOklch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var body = text.Trim();
        if (body.StartsWith("oklch", StringComparison.OrdinalIgnoreCase))
            body = body.Substring(5);
        body = body.Trim().TrimStart('(').TrimEnd(')');

        double? alpha = null;
        var slash = body.IndexOf('/');
        if (slash >= 0)
        {
            if (!TryParseNumber(body.Substring(slash + 1), out var a, out var alphaIsPercent))
                return false;
            alpha = alphaIsPercent ? a / 100 : a;
            body = body.Substring(0, slash);
        }

        var parts = body.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !TryParseNumber(parts[0], out var lightness, out var lightnessIsPercent)
            || !TryParseNumber(parts[1], out var chroma, out var chromaIsPercent)
            || !TryParseNumber(parts[2].Replace("deg", "", StringComparison.OrdinalIgnoreCase), out var hue, out _))
            return false;

        // L: "62.8%" или 0.628; число больше 1 без % тоже считаем процентами
        if (lightnessIsPercent || lightness > 1)
            lightness /= 100;
        // C в процентах по CSS: 100% = 0.4
        if (chromaIsPercent)
            chroma = chroma / 100 * 0.4;

        if (alpha.HasValue)
            _alpha = Math.Clamp(alpha.Value, 0, 1);
        SetOklch(lightness * 100, chroma, hue, force: true);
        return true;
    }

    private static bool TryParseNumber(string text, out double value, out bool isPercent)
    {
        text = text.Trim();
        isPercent = text.EndsWith('%');
        if (isPercent)
            text = text.Substring(0, text.Length - 1);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    #endregion

    #region Контраст WCAG

    /// <summary>Фон для проверки контраста; текущий цвет - это текст.</summary>
    public string ContrastBackgroundHex
    {
        get => ColorFormat.Hex(_contrastBackground);
        set
        {
            var hex = value?.Trim().TrimStart('#') ?? "";
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                return;
            SetContrastBackground(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        }
    }

    public IBrush ContrastBackgroundBrush => new ImmutableSolidColorBrush(_contrastBackground);

    public double ContrastRatio => ColorMath.ContrastRatio(SelectedColor, _contrastBackground);

    public string ContrastRatioText => string.Format(CultureInfo.InvariantCulture, "{0:0.00} : 1", ContrastRatio);

    // Пороговые значения WCAG 2.2 (критерии 1.4.3, 1.4.6, 1.4.11)
    public bool PassAaNormal => ContrastRatio >= 4.5;
    public bool PassAaaNormal => ContrastRatio >= 7;
    public bool PassAaLarge => ContrastRatio >= 3;
    public bool PassAaaLarge => ContrastRatio >= 4.5;
    public bool PassUi => ContrastRatio >= 3;

    /// <summary>Сделать текущий цвет фоном (дальше можно выбрать цвет текста пипеткой).</summary>
    public void UseCurrentAsContrastBackground() => SetContrastBackground(OpaqueColor);

    /// <summary>Поменять местами цвет текста и фон.</summary>
    public void SwapContrastColors()
    {
        var oldBackground = _contrastBackground;
        SetContrastBackground(OpaqueColor);
        SetColor(Color.FromArgb((byte)Math.Round(_alpha * 255), oldBackground.R, oldBackground.G, oldBackground.B));
    }

    #region Подбор под контраст

    private int _contrastTargetIndex = App.Settings.ContrastTargetIndex;
    private string? _contrastSuggestionText;

    /// <summary>0 - AA (4.5), 1 - AAA (7), 2 - крупный текст AA (3).</summary>
    public int ContrastTargetIndex
    {
        get => _contrastTargetIndex;
        set
        {
            if (!this.RaiseAndSetIfChanged(ref _contrastTargetIndex, Math.Clamp(value, 0, 2)))
                return;
            App.Settings.ContrastTargetIndex = _contrastTargetIndex;
            App.Settings.Save();
        }
    }

    /// <summary>Итог последнего подбора (что изменилось и какой получился контраст).</summary>
    public string? ContrastSuggestionText
    {
        get => _contrastSuggestionText;
        private set => this.RaiseAndSetIfChanged(ref _contrastSuggestionText, value);
    }

    private double ContrastTarget => _contrastTargetIndex switch
    {
        1 => 7,
        2 => 3,
        _ => 4.5,
    };

    private string ContrastTargetName => _contrastTargetIndex switch
    {
        1 => Loc.T("contrast_name_aaa"),
        2 => Loc.T("contrast_name_large"),
        _ => Loc.T("contrast_name_aa"),
    };

    /// <summary>Ближайший цвет текста, проходящий выбранный уровень (меняется только светлота).</summary>
    public void SuggestTextColor()
    {
        var target = ContrastTarget;
        var background = _contrastBackground;
        var alpha = (byte)Math.Round(_alpha * 255);

        if (ContrastRatio >= target)
        {
            ContrastSuggestionText = Loc.F("contrast_text_passes", ContrastTargetName);
            return;
        }

        var before = ColorMath.ToOklch(OpaqueColor).L;
        var (color, reached, ratio) = ContrastFix.Find(
            OpaqueColor,
            c => ColorMath.ContrastRatio(Color.FromArgb(alpha, c.R, c.G, c.B), background),
            target);

        SetColor(Color.FromArgb(alpha, color.R, color.G, color.B));
        ContrastSuggestionText = DescribeSuggestion(Loc.T("contrast_what_text"), before, color, reached, ratio);
    }

    /// <summary>Ближайший цвет фона, с которым текущий текст проходит выбранный уровень.</summary>
    public void SuggestBackgroundColor()
    {
        var target = ContrastTarget;
        var text = SelectedColor;

        if (ContrastRatio >= target)
        {
            ContrastSuggestionText = Loc.F("contrast_pair_passes", ContrastTargetName);
            return;
        }

        var before = ColorMath.ToOklch(_contrastBackground).L;
        var (color, reached, ratio) = ContrastFix.Find(
            _contrastBackground,
            c => ColorMath.ContrastRatio(text, c),
            target);

        SetContrastBackground(color);
        ContrastSuggestionText = DescribeSuggestion(Loc.T("contrast_what_bg"), before, color, reached, ratio);
    }

    private string DescribeSuggestion(string what, double lightnessBefore, Color result, bool reached, double ratio)
    {
        var after = ColorMath.ToOklch(result).L;
        return reached
            ? Loc.F("contrast_result_ok", what, lightnessBefore * 100, after * 100, ColorFormat.Hex(result), ratio, ContrastTargetName)
            : Loc.F("contrast_result_fail", what, ContrastTargetName, ratio, ColorFormat.Hex(result));
    }

    #endregion

    private void SetContrastBackground(Color color)
    {
        _contrastBackground = color;
        App.Settings.ContrastBackground = ColorMath.ToHex(color);
        App.Settings.Save();

        RaisePropertyChanged(nameof(ContrastBackgroundHex));
        RaisePropertyChanged(nameof(ContrastBackgroundBrush));
        RaisePropertyChanged(nameof(ContrastRatioText));
        RaisePropertyChanged(nameof(PassAaNormal));
        RaisePropertyChanged(nameof(PassAaaNormal));
        RaisePropertyChanged(nameof(PassAaLarge));
        RaisePropertyChanged(nameof(PassAaaLarge));
        RaisePropertyChanged(nameof(PassUi));
    }

    #endregion

    public int CopyFormatIndex
    {
        get => _copyFormatIndex;
        set
        {
            if (!this.RaiseAndSetIfChanged(ref _copyFormatIndex, value))
                return;
            App.Settings.CopyFormatIndex = value;
            App.Settings.Save();
        }
    }

    public string CopyFormat
    {
        get => _copyFormat;
        set => this.RaiseAndSetIfChanged(ref _copyFormat, value);
    }

    public bool CopyWithAlpha
    {
        get => _copyWithAlpha;
        set
        {
            if (!this.RaiseAndSetIfChanged(ref _copyWithAlpha, value))
                return;
            App.Settings.IsCopyWithAlpha = value;
            App.Settings.Save();
        }
    }

    public Color SelectedColor => Color.FromArgb((byte)Math.Round(_alpha * 255), (byte)_r, (byte)_g, (byte)_b);
    public Color OpaqueColor => Color.FromArgb(255, (byte)_r, (byte)_g, (byte)_b);
    public IBrush SelectedBrush => new ImmutableSolidColorBrush(SelectedColor);

    public string GetColorAsText()
    {
        // Всегда точка как разделитель дробной части: "rgba(255, 0, 0, 0.50)", а не "0,50"
        var inv = CultureInfo.InvariantCulture;
        return CopyFormat switch
        {
            "hsv" => CopyWithAlpha
                ? string.Format(inv, "hsva({0:F0}, {1:F0}%, {2:F0}%, {3:F2})", Hue, Saturation * 100, Value * 100, Alpha)
                : string.Format(inv, "hsv({0:F0}, {1:F0}%, {2:F0}%)", Hue, Saturation * 100, Value * 100),
            "normal" => CopyWithAlpha
                ? string.Format(inv, "({0:F2}, {1:F2}, {2:F2}, {3:F2})", NormalR, NormalG, NormalB, Alpha)
                : string.Format(inv, "({0:F2}, {1:F2}, {2:F2})", NormalR, NormalG, NormalB),
            "hex" => FormatHex(AlphaByte, R, G, B, CopyWithAlpha),
            "rgb-565" => RGB565.ToString(inv),
            "oklch" => FormatOklch(CopyWithAlpha),
            _ => CopyWithAlpha
                ? string.Format(inv, "rgba({0}, {1}, {2}, {3:F2})", R, G, B, Alpha)
                : string.Format(inv, "rgb({0}, {1}, {2})", R, G, B),
        };
    }

    public void SetColor(Color color)
    {
        var (h, s, v) = KeepUndefinedHsv(ColorMath.ToHsv(color));
        SetFromHsv(h, s, v, color.A / 255.0);
    }

    /// <summary>
    /// У серых тон не определён, у чёрного - ещё и насыщенность. Вместо 0 (красный) оставляем
    /// текущие значения, чтобы маркеры круга не прыгали и цвет можно было «вернуть» обратно.
    /// </summary>
    private (double H, double S, double V) KeepUndefinedHsv((double H, double S, double V) hsv)
    {
        var (h, s, v) = hsv;
        if (v <= 0)
            return (_hue, _saturation, 0);
        if (s <= 0)
            return (_hue, 0, v);
        return (h, s, v);
    }

    private void UpdateFromHsv()
    {
        if (_internalUpdate)
            return;
        SetFromHsv(_hue, _saturation, _value, _alpha);
    }

    private void UpdateFromRgb()
    {
        if (_internalUpdate)
            return;

        _internalUpdate = true;
        var color = Color.FromArgb((byte)Math.Round(_alpha * 255), (byte)_r, (byte)_g, (byte)_b);
        (_hue, _saturation, _value) = KeepUndefinedHsv(ColorMath.ToHsv(color));
        UpdateDerived();
        RaiseAll();
        _internalUpdate = false;
    }

    private void SetFromHsv(double h, double s, double v, double a)
    {
        _internalUpdate = true;
        _hue = WrapHue(h);
        _saturation = Math.Clamp(s, 0, 1);
        _value = Math.Clamp(v, 0, 1);
        _alpha = Math.Clamp(a, 0, 1);

        var color = ColorMath.FromHsv(_hue, _saturation, _value, _alpha);
        _r = color.R;
        _g = color.G;
        _b = color.B;
        UpdateDerived();
        RaiseAll();
        _internalUpdate = false;
    }

    private void UpdateDerived()
    {
        // Поле Hex показывает альфу, только если цвет полупрозрачный
        _hex = FormatHex(AlphaByte, _r, _g, _b, AlphaByte < 255);
        _rgb565 = ((_r & 0xF8) << 8) | ((_g & 0xFC) << 3) | (_b >> 3);
        SyncOklchFromRgb();
    }

    private void TryUpdateFromHex(string? text)
    {
        if (_internalUpdate || string.IsNullOrWhiteSpace(text))
            return;

        var value = text.Trim().TrimStart('#');
        if (value.Length is not (6 or 8)
            || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            return;

        uint rgb;
        if (value.Length == 8)
        {
            // 8 цифр - с альфой, в порядке из настроек
            uint alpha;
            if (App.Settings.HexAlphaFirst)
            {
                alpha = number >> 24;
                rgb = number & 0xFFFFFF;
            }
            else
            {
                alpha = number & 0xFF;
                rgb = number >> 8;
            }
            _alpha = alpha / 255.0;
        }
        else
        {
            rgb = number;
        }

        _r = (int)((rgb >> 16) & 0xFF);
        _g = (int)((rgb >> 8) & 0xFF);
        _b = (int)(rgb & 0xFF);
        UpdateFromRgb();
    }

    private static double WrapHue(double hue)
    {
        var h = hue % 360;
        if (h < 0) h += 360;
        return h;
    }

    private void RaiseAll()
    {
        foreach (var args in AllColorProperties)
            RaisePropertyChanged(args);
    }

    // Команды трея (привязаны к методам в App.axaml)
    public void ShowWindowCommand() => App.ShowMainWindow();

    public void ShowEyedropper() => App.GetOrCreateMainWindow().ShowEyedropper();

    public void QuitCommand() => App.Quit();
}
