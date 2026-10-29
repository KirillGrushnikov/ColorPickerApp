using Avalonia;
using Avalonia.Media;
using System;

namespace ColorPickerApp;

public static class StartupHelper
{
    public static bool IsAutoStartMode { get; private set; }

    public static void Initialize(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "/auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-auto", StringComparison.OrdinalIgnoreCase))
            {
                IsAutoStartMode = true;
                return;
            }
        }
    }
}

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupHelper.Initialize(args);

        // Программа уже запущена (например, свёрнута в трей) - просто показываем её окно,
        // вместо холодного старта второй копии с ещё одним хуком и иконкой в трее.
        if (!SingleInstance.TryAcquire())
        {
            if (!StartupHelper.IsAutoStartMode)
                SingleInstance.ActivateExisting();
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Системный шрифт Windows: не нужно грузить встроенный Inter (~1.9 МБ) при запуске
            .With(new FontManagerOptions { DefaultFamilyName = "Segoe UI" })
            .LogToTrace();
}
