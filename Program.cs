using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.Linq;

namespace ColorPickerApp;

public static class StartupHelper
{
    public static bool IsAutoStartMode { get; private set; }

    public static void Initialize(string[] args)
    {
        IsAutoStartMode = args.Contains("/auto") || args.Contains("-auto");
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

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
