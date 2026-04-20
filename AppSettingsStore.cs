using Avalonia.Input;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorPickerApp;

public sealed class AppSettingsStore : ReactiveObject
{
    private const string SettingsFileName = "settings.json";

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
        set => this.RaiseAndSetIfChanged(ref _pickerHotkey, value);
    }

    public string OpenWindowHotkey
    {
        get => _openWindowHotkey;
        set => this.RaiseAndSetIfChanged(ref _openWindowHotkey, value);
    }

    public bool RestoreWindowAfterPick { get; set; } = false;

    public int TransparentBackgroundIndex { get; set; } = 0;


    private string _transparentColor1 = "#FFFFFF";
    private string _transparentColor2 = "#C1C1C1";

    public string TransparentColor1 
    { 
        get => _transparentColor1;
        set
        {
            _transparentColor1 = value;
            this.RaisePropertyChanged(nameof(TransparentBackground1));
        }
    }

    public string TransparentColor2
    {
        get => _transparentColor2;
        set
        {
            _transparentColor2 = value;
            this.RaisePropertyChanged(nameof(TransparentBackground2));
        }
    }

    // Кисти, доступные для привязки в UI, но не сериализуемые
    [JsonIgnore]
    public SolidColorBrush TransparentBackground1 => 
        new SolidColorBrush(Color.Parse(TransparentColor1));

    [JsonIgnore]
    public SolidColorBrush TransparentBackground2 =>
        new SolidColorBrush(Color.Parse(TransparentColor2));

    public int CopyFormatIndex { get; set; } = 0;


    public static AppSettingsStore Load()
    {
        try
        {
            var file = GetFilePath();
            if (!File.Exists(file))
                return new AppSettingsStore();

            var json = File.ReadAllText(file);
            var settings = JsonSerializer.Deserialize<AppSettingsStore>(json);
            var loaded = settings ?? new AppSettingsStore();
            loaded.SavedSwatches ??= new List<string>();
            return loaded;
        }
        catch
        {
            return new AppSettingsStore();
        }
    }

    public void Save()
    {
        try
        {
            var file = GetFilePath();
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch
        {
            // Ignore save errors to avoid breaking UI flow.
        }
    }

    private static string GetFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ColorPickerApp", SettingsFileName);
    }
}
