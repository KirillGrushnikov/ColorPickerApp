using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ColorPickerApp.Services;
using ColorPickerApp.ViewModels;
using ColorPickerApp.Views;

namespace ColorPickerApp;

public partial class App : Application
{
    private static bool _quitting;

    public static AppSettingsStore Settings { get; private set; } = new();

    public static GlobalHotkeyService Hotkeys { get; private set; } = null!;

    public static MainWindowViewModel MainViewModel { get; private set; } = null!;

    public override void Initialize()
    {
        Settings = AppSettingsStore.Load();
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        //this.AttachDeveloperTools(o =>
        //{
        //    o.ConnectOnStartup = true;
        //});
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Приложение живёт в трее, поэтому завершаем его только явно (App.Quit)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) =>
            {
                Hotkeys.Dispose();
                Settings.Flush();
            };

            MainViewModel = new MainWindowViewModel();
            DataContext = MainViewModel; // для привязок TrayIcon

            Hotkeys = new GlobalHotkeyService();
            Hotkeys.OpenWindowPressed += () => Dispatcher.UIThread.Post(ShowMainWindow);
            Hotkeys.PickerPressed += () => Dispatcher.UIThread.Post(() => GetOrCreateMainWindow().ShowEyedropper());
            Hotkeys.StatusChanged += () => Dispatcher.UIThread.Post(MainViewModel.RefreshHotkeyStatus);

            SingleInstance.ListenForActivation(() => Dispatcher.UIThread.Post(ShowMainWindow));

            if (StartupHelper.IsAutoStartMode)
            {
                // Автозапуск с Windows: окно не создаём вовсе, только иконка в трее и хоткеи.
                // Окно появится при первом обращении (трей, хоткей, повторный запуск).
                Hotkeys.Start();
            }
            else
            {
                desktop.MainWindow = new MainWindow { DataContext = MainViewModel };
                // Хоткеи регистрируем после первого кадра, чтобы не задерживать показ окна
                Dispatcher.UIThread.Post(Hotkeys.Start, DispatcherPriority.Background);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static MainWindow GetOrCreateMainWindow()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!;
        if (desktop.MainWindow is MainWindow existing)
            return existing;

        var window = new MainWindow { DataContext = MainViewModel };
        desktop.MainWindow = window;
        return window;
    }

    public static void ShowMainWindow() => GetOrCreateMainWindow().Open();

    public static void Quit()
    {
        if (_quitting)
            return;
        _quitting = true;

        Settings.Flush();
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
