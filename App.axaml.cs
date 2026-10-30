using Avalonia;
using System;
using System.Diagnostics;
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
        Loc.Init(Settings.Language); // до загрузки XAML: разметка сразу берёт строки нужного языка
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
                SaveSessionState();
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
#if DEBUG
            else if (StartupHelper.SnapshotDir is { } snapshotDir)
            {
                AppSettingsStore.SavingDisabled = true;
                var window = new MainWindow { DataContext = MainViewModel };
                desktop.MainWindow = window;
                window.Opened += async (_, _) =>
                {
                    await LayoutSnapshots.RunAsync(window, snapshotDir);
                    Quit();
                };
            }
#endif
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

        SaveSessionState();
        Settings.Flush();
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    /// <summary>Запоминает последний цвет (если включено в настройках).</summary>
    public static void SaveSessionState()
    {
        if (!Settings.RememberLastColor || MainViewModel is null)
            return;

        var c = MainViewModel.SelectedColor;
        var hex = $"#{c.A:x2}{c.R:x2}{c.G:x2}{c.B:x2}"; // #AARRGGBB - формат Color.TryParse
        if (Settings.LastColor == hex)
            return;
        Settings.LastColor = hex;
        Settings.Save();
    }

    /// <summary>Перезапуск (смена языка): освобождаем хоткеи и «единственный экземпляр», запускаем новую копию.</summary>
    public static void Restart()
    {
        try
        {
            SaveSessionState();
            Settings.Flush();
            Hotkeys.Dispose();          // иначе новая копия не сможет зарегистрировать те же хоткеи
            SingleInstance.Release();   // иначе новая копия решит, что программа уже запущена

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }

        Quit();
    }
}
