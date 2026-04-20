using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ColorPickerApp.Services;
using ColorPickerApp.ViewModels;
using ColorPickerApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ColorPickerApp;

public partial class App : Application
{
    public static AppSettingsStore Settings { get; private set; } = new();

    public static ServiceProvider ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        //this.AttachDeveloperTools(o =>
        //{
        //    o.ConnectOnStartup = true;
        //});
#endif

        Settings = AppSettingsStore.Load();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GlobalHotkeyService>();

        ServiceProvider = services.BuildServiceProvider();

        var hotkeyService = ServiceProvider.GetRequiredService<GlobalHotkeyService>();
        hotkeyService.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RequestedThemeVariant = ThemeVariant.Dark;

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            DataContext = desktop.MainWindow.DataContext;
        }

        base.OnFrameworkInitializationCompleted();
    }

}