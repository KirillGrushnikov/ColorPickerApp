using Microsoft.Win32;
using System;
using System.IO;

namespace ColorPickerApp;

/// <summary>
/// Автозапуск через HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Раньше создавался .lnk через COM (пакет WindowsShortcutFactory) - это лишняя зависимость
/// и COM-интероп, несовместимый с Native AOT.
/// </summary>
public static class StartupManager
{
    private const string AppName = "Color Picker Pro";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Enable()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            key.SetValue(AppName, $"\"{exePath}\" /auto");
            RemoveLegacyShortcut();
        }
        catch
        {
            // Нет прав / политика запрещает - не роняем UI
        }
    }

    public static void Disable()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
        }

        RemoveLegacyShortcut();
    }

    // Ярлык в папке "Автозагрузка", который создавали прошлые версии, - иначе было бы два автозапуска
    private static void RemoveLegacyShortcut()
    {
        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = Path.Combine(startupFolder, $"{AppName}.lnk");
            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);
        }
        catch
        {
        }
    }
}
