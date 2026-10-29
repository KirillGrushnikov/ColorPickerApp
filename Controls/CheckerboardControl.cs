using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.ComponentModel;

namespace ColorPickerApp.Controls;

public class CheckerboardControl : Control
{
    public static readonly StyledProperty<int> CellSizeProperty =
        AvaloniaProperty.Register<CheckerboardControl, int>(nameof(CellSize), 10);

    static CheckerboardControl()
    {
        AffectsRender<CheckerboardControl>(CellSizeProperty);
    }

    public int CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    // Подписка только пока контрол на экране: раньше подписка в конструкторе на статические настройки
    // никогда не снималась, и каждый пересозданный свотч оставался в памяти навсегда.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        App.Settings.PropertyChanged += OnSettingsChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        App.Settings.PropertyChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettingsStore.TransparentBackground1) or nameof(AppSettingsStore.TransparentBackground2))
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = CellSize < 2 ? 2 : CellSize;
        var light = App.Settings.TransparentBackground1;
        var dark = App.Settings.TransparentBackground2;

        // Светлый цвет - одной заливкой, сверху только тёмные клетки (вдвое меньше вызовов отрисовки)
        context.DrawRectangle(light, null, new Rect(Bounds.Size));
        if (light.Color == dark.Color)
            return;

        for (var y = 0; y < Bounds.Height; y += size)
        {
            for (var x = 0; x < Bounds.Width; x += size)
            {
                if (((x / size) + (y / size)) % 2 == 0)
                    context.DrawRectangle(dark, null, new Rect(x, y, size, size));
            }
        }
    }
}
