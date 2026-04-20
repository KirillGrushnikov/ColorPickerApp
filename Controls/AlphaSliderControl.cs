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
        AvaloniaProperty.Register<AlphaSliderControl, double>(nameof(Alpha), 1.0);

    private bool _dragging;

    static AlphaSliderControl()
    {
        AffectsRender<AlphaSliderControl>(BaseColorProperty, AlphaProperty);
    }

    public AlphaSliderControl()
    {
        App.Settings.PropertyChanged += (s, e) => InvalidateVisual();
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

        // »спользуем границы как есть, без Ceiling
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // ѕриводим к целым координатам дл€ устранени€ субпиксельного дрожани€
        var rect = new Rect(
            0,
            0,
            Math.Ceiling(bounds.Width),
            Math.Ceiling(bounds.Height));

        DrawCheckerboard(context, rect);
        DrawAlphaGradient(context, rect);
        DrawAlphaIndicator(context, rect);
    }

    private void DrawCheckerboard(DrawingContext context, Rect rect)
    {
        // ‘иксированный размер клетки в DIP, кратный целому числу
        double cellSize = rect.Width / 3.0; // можно подобрать под дизайн

        var light = App.Settings.TransparentBackground1;
        var dark = App.Settings.TransparentBackground2;

        // –исуем только целое количество клеток, чтобы избежать обрезанных половинок
        int cols = (int)Math.Ceiling(rect.Width / cellSize);
        int rows = (int)Math.Ceiling(rect.Height / cellSize);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                bool useDark = (row + col) % 2 == 0;
                var cellRect = new Rect(
                    rect.X + col * cellSize,
                    rect.Y + row * cellSize,
                    cellSize,
                    cellSize);

                // ќбрезаем последнюю клетку по границе контрола
                cellRect = cellRect.Intersect(rect);
                if (cellRect.Width > 0 && cellRect.Height > 0)
                {
                    context.DrawRectangle(useDark ? dark : light, null, cellRect);
                }
            }
        }
    }

    private void DrawAlphaGradient(DrawingContext context, Rect rect)
    {
        var topColor = Color.FromArgb(255, BaseColor.R, BaseColor.G, BaseColor.B);
        var bottomColor = Color.FromArgb(0, BaseColor.R, BaseColor.G, BaseColor.B);

        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(topColor, 0.0),
                new(bottomColor, 1.0)
            }
        };

        context.DrawRectangle(gradientBrush, null, rect);
    }

    private void DrawAlphaIndicator(DrawingContext context, Rect rect)
    {
        double y = rect.Y + (1 - ColorMath.Clamp01(Alpha)) * rect.Height;

        // –исуем два небольших круга по бокам дл€ нагл€дности положени€ ползунка
        var leftCenter = new Point(rect.X - 4, y);
        var rightCenter = new Point(rect.X + rect.Width + 4, y);

        var fillBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        var pen = new Pen(Brushes.White, 1.2);

        context.DrawEllipse(fillBrush, pen, leftCenter, 2, 2);
        context.DrawEllipse(fillBrush, pen, rightCenter, 2, 2);
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

        double relativeY = (point.Y - Bounds.Y) / Bounds.Height;
        Alpha = ColorMath.Clamp01(1 - relativeY);
        InvalidateVisual();
    }
}