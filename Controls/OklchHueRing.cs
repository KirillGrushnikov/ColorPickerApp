using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace ColorPickerApp.Controls;

/// <summary>
/// Круг тона OKLCH: кольцо показывает все тона при текущих светлоте и хроме
/// (затемнены те, что не помещаются в sRGB), в центре - текущий цвет.
/// Тон выбирается кликом/перетаскиванием в любом месте круга, колесом мыши или стрелками
/// (Shift - шаг 10°). 0° сверху, по часовой стрелке - как у основного круга.
/// </summary>
public class OklchHueRing : Control
{
    /// <summary>Светлота в процентах 0..100.</summary>
    public static readonly StyledProperty<double> LightnessProperty =
        AvaloniaProperty.Register<OklchHueRing, double>(nameof(Lightness), 50);

    public static readonly StyledProperty<double> ChromaProperty =
        AvaloniaProperty.Register<OklchHueRing, double>(nameof(Chroma));

    public static readonly StyledProperty<double> HueAngleProperty =
        AvaloniaProperty.Register<OklchHueRing, double>(nameof(HueAngle));

    private const double RingThickness = 20;
    private const int HueBins = 720; // кольцо считается по 720 тонам, а не по каждому пикселю

    private static readonly IPen MarkerOuterPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
    private static readonly IPen MarkerInnerPen = new ImmutablePen(Brushes.White, 2);
    private static readonly IPen CenterPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
    private static readonly IPen FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);

    private WriteableBitmap? _ring;
    private (int Size, double Scaling, double L, double C) _ringKey;
    private bool _dragging;
    private bool _showFocus;

    static OklchHueRing()
    {
        AffectsRender<OklchHueRing>(LightnessProperty, ChromaProperty, HueAngleProperty);
        FocusableProperty.OverrideDefaultValue<OklchHueRing>(true);
    }

    public OklchHueRing()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public double Lightness
    {
        get => GetValue(LightnessProperty);
        set => SetValue(LightnessProperty, value);
    }

    public double Chroma
    {
        get => GetValue(ChromaProperty);
        set => SetValue(ChromaProperty, value);
    }

    public double HueAngle
    {
        get => GetValue(HueAngleProperty);
        set => SetValue(HueAngleProperty, value);
    }

    private double Diameter => Math.Min(Bounds.Width, Bounds.Height);
    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var diameter = Diameter;
        if (diameter < 2 * RingThickness + 4)
            return;

        var center = Center;
        var outer = diameter / 2 - 1;
        var inner = outer - RingThickness;

        // 1. Кольцо тонов (кэш)
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var sizePx = Math.Max(1, (int)Math.Ceiling(diameter * scaling));
        EnsureRing(sizePx, scaling, outer, inner);
        var ringSizeDip = sizePx / scaling;
        context.DrawImage(_ring!, new Rect(center.X - ringSizeDip / 2, center.Y - ringSizeDip / 2, ringSizeDip, ringSizeDip));

        // 2. Текущий цвет в центре
        var current = new ImmutableSolidColorBrush(ColorMath.FromOklch(Lightness / 100, Chroma, HueAngle));
        context.DrawEllipse(current, CenterPen, center, inner - 8, inner - 8);

        // 3. Маркер выбранного тона на кольце
        var radians = (HueAngle - 90) * Math.PI / 180;
        var markerRadius = outer - RingThickness / 2;
        var marker = new Point(center.X + Math.Cos(radians) * markerRadius, center.Y + Math.Sin(radians) * markerRadius);
        context.DrawEllipse(current, MarkerOuterPen, marker, 9, 9);
        context.DrawEllipse(null, MarkerInnerPen, marker, 7.5, 7.5);
        if (_showFocus)
            context.DrawEllipse(null, FocusPen, marker, 12, 12);
    }

    private unsafe void EnsureRing(int sizePx, double scaling, double outerDip, double innerDip)
    {
        var key = (sizePx, scaling, Lightness, Chroma);
        if (_ring != null && key == _ringKey)
            return;

        // Цвета для всех тонов при текущих L и C
        var l = Lightness / 100;
        var c = Chroma;
        var bins = new uint[HueBins];
        for (var i = 0; i < HueBins; i++)
            bins[i] = OklchPaint.ToPixel(l, c, (i + 0.5) * 360.0 / HueBins);

        _ring?.Dispose();
        _ring = new WriteableBitmap(new PixelSize(sizePx, sizePx), new Vector(96, 96), PixelFormat.Bgra8888);
        _ringKey = key;

        var half = sizePx / 2.0;
        var outer = outerDip * scaling;
        var inner = innerDip * scaling;

        using var frame = _ring.Lock();
        var basePtr = (byte*)frame.Address;
        for (var y = 0; y < sizePx; y++)
        {
            var row = (uint*)(basePtr + y * frame.RowBytes);
            var dy = y + 0.5 - half;
            for (var x = 0; x < sizePx; x++)
            {
                var dx = x + 0.5 - half;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var coverage = Math.Clamp(outer - distance + 0.5, 0, 1) * Math.Clamp(distance - inner + 0.5, 0, 1);
                if (coverage <= 0)
                {
                    row[x] = 0;
                    continue;
                }

                var hue = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
                if (hue < 0) hue += 360;
                var pixel = bins[Math.Min(HueBins - 1, (int)(hue * HueBins / 360))];

                if (coverage < 1)
                {
                    // Сглаживание края: масштабируем все премультиплицированные каналы
                    var k = (uint)Math.Round(coverage * 255);
                    pixel = (((pixel >> 24) * k / 255) << 24)
                            | ((((pixel >> 16) & 0xFF) * k / 255) << 16)
                            | ((((pixel >> 8) & 0xFF) * k / 255) << 8)
                            | ((pixel & 0xFF) * k / 255);
                }

                row[x] = pixel;
            }
        }
    }

    private void SetHueFromPoint(Point point)
    {
        var center = Center;
        var hue = Math.Atan2(point.Y - center.Y, point.X - center.X) * 180 / Math.PI + 90;
        if (hue < 0) hue += 360;
        if (hue >= 360) hue -= 360;
        HueAngle = hue;
    }

    private void Step(double delta)
    {
        var hue = (HueAngle + delta) % 360;
        if (hue < 0) hue += 360;
        HueAngle = hue;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _dragging = true;
        e.Pointer.Capture(this);
        Focus();
        SetHueFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            SetHueFromPoint(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    // Рамку фокуса показываем только при переходе с клавиатуры (Tab), а не после клика мышью -
    // иначе маркер выглядел «залипшим» выбранным до смены вкладки
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _showFocus = e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _showFocus = false;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (delta == 0)
            return;

        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        Step(delta > 0 ? step : -step);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Step(-step);
                break;
            case Key.Right:
            case Key.Up:
                Step(step);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }

        e.Handled = true;
    }
}
