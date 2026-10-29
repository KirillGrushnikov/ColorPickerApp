using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

    /// <summary>Индекс уровня увеличения лупы пипетки.</summary>
    public int EyedropperZoomIndex { get; set; } = 3;

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

    public void Flush()
    {
        _saveTimer?.Stop();
        if (!_dirty)
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
