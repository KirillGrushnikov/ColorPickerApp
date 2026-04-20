using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using WindowsShortcutFactory;

public static class StartupManager
{
    private static readonly string AppName = "Color Picker Pro";

    public static void AddToStartupViaShortcut()
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string shortcutPath = Path.Combine(startupFolder, $"{AppName}.lnk");

        if (!File.Exists(shortcutPath))
        {
            CreateShortcut(shortcutPath, Environment.ProcessPath, "/auto");
        }
    }

    public static void RemoveFromStartupViaShortcut()
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string shortcutPath = Path.Combine(startupFolder, $"{AppName}.lnk");

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments)
    {
        var shortcut = new WindowsShortcut
        {
            Path = targetPath,
            Arguments = arguments,
            WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath)
        };
        shortcut.Save(shortcutPath);
    }
}