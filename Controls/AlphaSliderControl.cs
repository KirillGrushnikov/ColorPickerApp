using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ColorPickerApp.Controls;

public class AlphaSliderControl : Control
{
    public static readonly StyledProperty<Color> BaseColorProperty =
        AvaloniaProperty.Register<AlphaSliderControl, Color>(nameof(BaseColor), Colors.White);

    public static readonly StyledProperty<double> AlphaProperty =
        AvaloniaProperty.Register<AlphaSliderControl, double>(nameof(Alpha), 1);

    private bool _dragging;
    
    static AlphaSliderControl()
    {
        AffectsRender<AlphaSliderControl>(BaseColorProperty, AlphaProperty);
    }

    public Color BaseColor
    {
        get => GetValue(BaseColorProperty);
        set => SetValue(BaseColorProperty, value);
    }

    public double Alpha
    {
        get => GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var localBounds = new Rect(0, 0, Math.Ceiling(Bounds.Width), Math.Ceiling(Bounds.Height));
        DrawCheckerboard(context, localBounds);

        var top = Color.FromArgb(255, BaseColor.R, BaseColor.G, BaseColor.B);
        var bottom = Color.FromArgb(0, BaseColor.R, BaseColor.G, BaseColor.B);
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new(top, 0),
                    new(bottom, 1),
                },
            },
            null,
            localBounds);

        var y = (1 - ColorMath.Clamp01(Alpha)) * localBounds.Height;
        var leftCenter = new Point(-8, y);
        var rightCenter = new Point(Math.Max(-8, localBounds.Width + 8), y);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), new Pen(Brushes.White, 1.2), leftCenter, 2, 2);
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), new Pen(Brushes.White, 1.2), rightCenter, 2, 2);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        e.Pointer.Capture(this);
        UpdateAlpha(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            UpdateAlpha(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void UpdateAlpha(Point point)
    {
        if (Bounds.Height <= 0)
            return;
        Alpha = ColorMath.Clamp01(1 - point.Y / Bounds.Height);
        InvalidateVisual();
    }

    private static void DrawCheckerboard(DrawingContext context, Rect rect)
    {
        double size = rect.Width / 3;
        var light = new SolidColorBrush(Color.FromRgb(238, 238, 238));
        var dark = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        for (var y = 0.0; y < rect.Height; y += size)
        {
            for (var x = 0.0; x < rect.Width; x += size)
            {
                var useDark = ((x / size) + (y / size)) % 2 == 0;
                context.DrawRectangle(useDark ? dark : light, null, new Rect(x, y, size, size));
            }
        }
    }
}
