using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ColorPickerApp.Controls;

public class CheckerboardControl : Control
{
    public static readonly StyledProperty<int> CellSizeProperty =
        AvaloniaProperty.Register<CheckerboardControl, int>(nameof(CellSize), 10);

    public CheckerboardControl()
    {
        App.Settings.PropertyChanged += (s, e) => InvalidateVisual();
    }

    public int CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var size = CellSize < 2 ? 2 : CellSize;
        var light = App.Settings.TransparentBackground1;
        var dark = App.Settings.TransparentBackground2;
        for (var y = 0; y < Bounds.Height; y += size)
        {
            for (var x = 0; x < Bounds.Width; x += size)
            {
                var useDark = ((x / size) + (y / size)) % 2 == 0;
                context.DrawRectangle(useDark ? dark : light, null, new Rect(x, y, size, size));
            }
        }
    }
}
