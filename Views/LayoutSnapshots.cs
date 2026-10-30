#if DEBUG
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;

namespace ColorPickerApp.Views;

/// <summary>
/// Только Debug: запуск с --snapshot "папка" рендерит окно в PNG в нескольких раскладках и закрывает программу.
/// Нужен, чтобы проверить вёрстку колонок без ручного прокликивания. Настройки при этом не сохраняются.
/// </summary>
internal static class LayoutSnapshots
{
    public static async Task RunAsync(MainWindow window, string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);

            await Shot(window, dir, "1-classic-contrast", WindowLayout.Classic, WindowLayout.Contrast);
            await Shot(window, dir, "2-classic-oklch", WindowLayout.Classic, WindowLayout.Oklch);
            await Shot(window, dir, "3-designer", WindowLayout.Designer, null);

            // Колонка слева, основная, две справа; «Справка» скрыта
            var wide = new WindowLayout(
                new[]
                {
                    new[] { WindowLayout.Oklch },
                    Array.Empty<string>(),
                    new[] { WindowLayout.Contrast },
                    new[] { WindowLayout.Scale, WindowLayout.History },
                },
                new[] { WindowLayout.Help },
                mainIndex: 1);
            await Shot(window, dir, "4-columns-scale", wide, WindowLayout.Scale);
            await Shot(window, dir, "5-columns-history", wide, WindowLayout.History);

            // Настройки: вкладка «Основные» должна помещаться без прокрутки, затем вкладка раскладки
            App.Settings.SetLayout(WindowLayout.Designer);
            window.OpenSettingsPaneForSnapshot();
            await Task.Delay(1200);
            Save(window, Path.Combine(dir, "6-settings.png"));

            window.SelectSettingsTabForSnapshot(1);
            await Task.Delay(700);
            Save(window, Path.Combine(dir, "7-settings-layout.png"));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString());
        }
    }

    private static async Task Shot(MainWindow window, string dir, string name, WindowLayout layout, string? selectTab)
    {
        App.Settings.SetLayout(layout);
        if (selectTab is not null)
            window.SelectTabForSnapshot(selectTab);
        await Task.Delay(700);
        Save(window, Path.Combine(dir, name + ".png"));
    }

    private static void Save(MainWindow window, string path)
    {
        var size = new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path);
    }
}
#endif
