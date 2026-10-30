using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorPickerApp;

public sealed class AppSettingsStore : INotifyPropertyChanged
{
    private const string SettingsFileName = "settings.json";
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private DispatcherTimer? _saveTimer;
    private bool _dirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsAutoStart { get; set; } = false;
    public bool IsCopyWithAlpha { get; set; } = true;
    public bool IsCopyWithFunction { get; set; } = true;
    public List<string> SavedSwatches { get; set; } = new();

    public bool PermanentCloseWindow { get; set; } = false;
    public bool AutoCopyOnPick { get; set; } = false;


    private string _pickerHotkey = "Ctrl+Shift+P";
    private string _openWindowHotkey = "Ctrl+Shift+O";

    public string PickerHotkey
    {
        get => _pickerHotkey;
        set => SetField(ref _pickerHotkey, value);
    }

    public string OpenWindowHotkey
    {
        get => _openWindowHotkey;
        set => SetField(ref _openWindowHotkey, value);
    }

    public bool RestoreWindowAfterPick { get; set; } = false;

    public int TransparentBackgroundIndex { get; set; } = 0;


    private string _transparentColor1 = "#FFFFFF";
    private string _transparentColor2 = "#C1C1C1";
    private ImmutableSolidColorBrush? _transparentBrush1;
    private ImmutableSolidColorBrush? _transparentBrush2;

    public string TransparentColor1
    {
        get => _transparentColor1;
        set
        {
            if (_transparentColor1 == value)
                return;
            _transparentColor1 = value;
            _transparentBrush1 = null;
            OnPropertyChanged(nameof(TransparentBackground1));
        }
    }

    public string TransparentColor2
    {
        get => _transparentColor2;
        set
        {
            if (_transparentColor2 == value)
                return;
            _transparentColor2 = value;
            _transparentBrush2 = null;
            OnPropertyChanged(nameof(TransparentBackground2));
        }
    }

    // Кисти для отрисовки шахматного фона. Создаются один раз на значение цвета,
    // а не при каждом Render каждого контрола, как раньше.
    [JsonIgnore]
    public ImmutableSolidColorBrush TransparentBackground1 =>
        _transparentBrush1 ??= CreateBrush(_transparentColor1, Colors.White);

    [JsonIgnore]
    public ImmutableSolidColorBrush TransparentBackground2 =>
        _transparentBrush2 ??= CreateBrush(_transparentColor2, Color.FromRgb(0xC1, 0xC1, 0xC1));

    public int CopyFormatIndex { get; set; } = 0;

    /// <summary>Второй цвет (фон) для проверки контраста WCAG.</summary>
    public string ContrastBackground { get; set; } = "#FFFFFF";

    private bool _hexUppercase;
    private bool _alwaysOnTop;

    /// <summary>Язык интерфейса: "ru", "en" или пусто - как в Windows. Применяется после перезапуска.</summary>
    public string Language { get; set; } = "";

    /// <summary>HEX заглавными буквами: #FF0000 вместо #ff0000.</summary>
    public bool HexUppercase
    {
        get => _hexUppercase;
        set
        {
            if (_hexUppercase == value)
                return;
            _hexUppercase = value;
            OnPropertyChanged(nameof(HexUppercase));
        }
    }

    /// <summary>Главное окно поверх всех окон.</summary>
    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (_alwaysOnTop == value)
                return;
            _alwaysOnTop = value;
            OnPropertyChanged(nameof(AlwaysOnTop));
        }
    }

    /// <summary>Открывать программу с последним выбранным цветом.</summary>
    public bool RememberLastColor { get; set; } = true;

    /// <summary>Последний цвет (#AARRGGBB), сохраняется при выходе.</summary>
    public string LastColor { get; set; } = "";

    private bool _hexAlphaFirst;

    /// <summary>
    /// Порядок альфы в HEX: false - #RRGGBBAA (CSS, веб, Figma), true - #AARRGGBB (Android, WPF/XAML, Avalonia).
    /// </summary>
    public bool HexAlphaFirst
    {
        get => _hexAlphaFirst;
        set
        {
            if (_hexAlphaFirst == value)
                return;
            _hexAlphaFirst = value;
            OnPropertyChanged(nameof(HexAlphaFirst));
        }
    }

    /// <summary>Уровень для подбора контраста: 0 - AA (4.5), 1 - AAA (7), 2 - крупный текст AA (3).</summary>
    public int ContrastTargetIndex { get; set; } = 0;

    /// <summary>Шкала оттенков: имя палитры, формат экспорта и формат значений.</summary>
    public string ScaleName { get; set; } = "primary";
    public int ScaleExportIndex { get; set; } = 0;
    public int ScaleValueIndex { get; set; } = 0;

    /// <summary>Индекс уровня увеличения лупы пипетки.</summary>
    public int EyedropperZoomIndex { get; set; } = 3;

    #region Раскладка окна

    /// <summary>Вкладки по колонкам слева направо; основная колонка (без «Значений») - под индексом LayoutMainIndex.</summary>
    public List<List<string>> LayoutColumns { get; set; } = new();

    /// <summary>Индекс основной колонки в LayoutColumns (сколько колонок слева от неё).</summary>
    public int LayoutMainIndex { get; set; }

    /// <summary>Открытые вкладки (по одной на колонку) - восстанавливаются при следующем запуске.</summary>
    public List<string> OpenTabs { get; set; } = new();

    /// <summary>Скрытые вкладки.</summary>
    public List<string> LayoutHidden { get; set; } = new();

    /// <summary>Текущая раскладка (каждый раз новая копия - её можно менять).</summary>
    [JsonIgnore]
    public WindowLayout Layout => new(LayoutColumns, LayoutHidden, LayoutMainIndex);

    /// <summary>Сохраняет раскладку; главное окно перестраивается сразу (PropertyChanged "Layout").</summary>
    public void SetLayout(WindowLayout layout)
    {
        if (layout.SameAs(Layout))
            return;

        LayoutColumns = layout.Columns.Select(c => c.ToList()).ToList();
        LayoutHidden = layout.Hidden.ToList();
        LayoutMainIndex = layout.MainIndex;
        Save();
        OnPropertyChanged(nameof(Layout));
    }

    #endregion

    #region История цветов

    public const int MaxRecentColors = 30;

    /// <summary>Последние цвета (#AARRGGBB), самый свежий первым.</summary>
    public List<string> RecentColors { get; set; } = new();

    /// <summary>История изменилась (вызывается в UI-потоке).</summary>
    public event Action? RecentColorsChanged;

    public void AddRecentColor(Color color)
    {
        var hex = $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
        if (RecentColors.Count > 0 && string.Equals(RecentColors[0], hex, StringComparison.OrdinalIgnoreCase))
            return;

        RecentColors.RemoveAll(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase));
        RecentColors.Insert(0, hex);
        if (RecentColors.Count > MaxRecentColors)
            RecentColors.RemoveRange(MaxRecentColors, RecentColors.Count - MaxRecentColors);

        Save();
        RecentColorsChanged?.Invoke();
    }

    public void RemoveRecentColor(int index)
    {
        if (index < 0 || index >= RecentColors.Count)
            return;
        RecentColors.RemoveAt(index);
        Save();
        RecentColorsChanged?.Invoke();
    }

    public void ClearRecentColors()
    {
        if (RecentColors.Count == 0)
            return;
        RecentColors.Clear();
        Save();
        RecentColorsChanged?.Invoke();
    }

    #endregion


    public static AppSettingsStore Load()
    {
        try
        {
            var file = GetFilePath();
            if (!File.Exists(file))
                return new AppSettingsStore();

            using var stream = File.OpenRead(file);
            var loaded = JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettingsStore)
                         ?? new AppSettingsStore();
            loaded.SavedSwatches ??= new List<string>();
            loaded.RecentColors ??= new List<string>();
            loaded.LayoutColumns ??= new List<List<string>>();
            loaded.LayoutHidden ??= new List<string>();
            loaded.OpenTabs ??= new List<string>();
            return loaded;
        }
        catch
        {
            return new AppSettingsStore();
        }
    }

    /// <summary>
    /// Отложенное сохранение: серия изменений подряд (например, при сбросе настроек)
    /// превращается в одну запись на диск. Немедленная запись - <see cref="Flush"/>.
    /// </summary>
    public void Save()
    {
        _dirty = true;
        if (_saveTimer is null)
        {
            _saveTimer = new DispatcherTimer { Interval = SaveDelay };
            _saveTimer.Tick += (_, _) => Flush();
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Не писать на диск (режим снимков в Debug не должен трогать настройки пользователя).</summary>
    public static bool SavingDisabled { get; set; }

    public void Flush()
    {
        _saveTimer?.Stop();
        if (!_dirty || SavingDisabled)
            return;
        _dirty = false;

        try
        {
            var file = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            // Пишем во временный файл и подменяем - настройки не испортятся при сбое во время записи
            var tempFile = file + ".tmp";
            File.WriteAllText(tempFile, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettingsStore));
            File.Move(tempFile, file, overwrite: true);
        }
        catch
        {
            // Ignore save errors to avoid breaking UI flow.
        }
    }

    private static ImmutableSolidColorBrush CreateBrush(string value, Color fallback) =>
        new(Color.TryParse(value, out var color) ? color : fallback);

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string GetFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ColorPickerApp", SettingsFileName);
    }
}

/// <summary>
/// Сериализатор, сгенерированный при компиляции: без reflection, быстрее на первом вызове
/// и совместим с trimming / Native AOT.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettingsStore))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
