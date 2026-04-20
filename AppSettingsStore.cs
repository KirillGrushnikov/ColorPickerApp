using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ColorPickerApp;

public sealed class AppSettingsStore
{
    private const string SettingsFileName = "settings.json";

    public bool IsCopyWithAlpha { get; set; } = true;
    public bool IsCopyWithFunction { get; set; } = true;
    public List<string> SavedSwatches { get; set; } = new();

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
