using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace ColorPickerApp.Controls;

public enum ColorHarmonyMode
{
    Single,
    Complementary,
    Triad,
    Analogous,
    AnalogousAccent,
    Tetrad
}

public class ColorWheelSvControl : Control
{
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorWheelSvControl, double>(nameof(Hue), 0);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ColorWheelSvControl, double>(nameof(Saturation), 1);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ColorWheelSvControl, double>(nameof(Value), 1);

    public static readonly StyledProperty<ColorHarmonyMode> HarmonyModeProperty =
        AvaloniaProperty.Register<ColorWheelSvControl, ColorHarmonyMode>(nameof(HarmonyMode), ColorHarmonyMode.Single);

    public static readonly StyledProperty<double> HarmonyAngleProperty =
        AvaloniaProperty.Register<ColorWheelSvControl, double>(nameof(HarmonyAngle), 30.0, coerce: CoerceHarmonyAngle);

    private bool _dragHue;
    private bool _dragSv;
    private bool _draggingMarker;
    private enum MarkerType { AnglePositive, AngleNegative, OppositeAngle }
    private MarkerType _draggedMarkerType;
    private double _initialHarmonyAngle;

    // Кольцо оттенков рендерится один раз в битмап и пересоздаётся только при смене размера/масштаба.
    // Раньше на каждый кадр (каждое движение мыши) рисовалось 720 линий с новыми Pen и Brush.
    private WriteableBitmap? _hueRingBitmap;
    private PixelSize _hueRingPixelSize;
    private double _hueRingScaling;

    private static readonly IBrush SvPointFill = new ImmutableSolidColorBrush(Color.FromArgb(90, 255, 255, 255));
    private static readonly IPen SvPointPen = new ImmutablePen(Brushes.White, 2);
    private static readonly IBrush MovableMarkerFill = new ImmutableSolidColorBrush(Color.FromArgb(180, 255, 255, 255));
    private static readonly IBrush PrimaryMarkerFill = new ImmutableSolidColorBrush(Color.FromArgb(50, 255, 255, 255));
    private static readonly IBrush FixedMarkerFill = new ImmutableSolidColorBrush(Color.FromArgb(180, 30, 30, 30));
    private static readonly IPen WhiteMarkerPen = new ImmutablePen(Brushes.White, 2);
    private static readonly IPen FixedMarkerPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(70, 70, 70)), 2);
    private static readonly IPen SvBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1);

    private static readonly IBrush SvWhiteGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Colors.White, 0),
            new(Color.FromArgb(0, 255, 255, 255), 1),
        },
    };

    private static readonly IBrush SvBlackGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Color.FromArgb(0, 0, 0, 0), 0),
            new(Colors.Black, 1),
        },
    };

    static ColorWheelSvControl()
    {
        AffectsRender<ColorWheelSvControl>(HueProperty, SaturationProperty, ValueProperty, HarmonyModeProperty, HarmonyAngleProperty);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public ColorHarmonyMode HarmonyMode
    {
        get => GetValue(HarmonyModeProperty);
        set => SetValue(HarmonyModeProperty, value);
    }

    public double HarmonyAngle
    {
        get => GetValue(HarmonyAngleProperty);
        set => SetValue(HarmonyAngleProperty, value);
    }

    private static double CoerceHarmonyAngle(AvaloniaObject obj, double value)
    {
        return Math.Clamp(value, 5, 90);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        var center = bounds.Center;
        var outerRadius = Math.Min(bounds.Width, bounds.Height) * 0.5 - 20;
        var ringThickness = 20.0;
        var innerRadius = outerRadius - ringThickness;

        DrawHueRing(context, center, outerRadius, ringThickness);

        var squareHalf = innerRadius * 0.68;
        var squareRect = new Rect(center.X - squareHalf, center.Y - squareHalf, squareHalf * 2, squareHalf * 2);
        DrawSvSquare(context, squareRect);

        DrawHueMarker(context, center, outerRadius, Hue, isPrimary: true, isMovable: false);
        DrawHarmonyMarkers(context, center, outerRadius);

        var svPoint = new Point(
            squareRect.Left + Saturation * squareRect.Width,
            squareRect.Top + (1 - Value) * squareRect.Height);
        context.DrawEllipse(SvPointFill, SvPointPen, svPoint, 6, 6);
    }

    public IEnumerable<Color> GetHarmonyColors()
    {
        switch (HarmonyMode)
        {
            case ColorHarmonyMode.Complementary:
                yield return ColorMath.FromHsv((Hue + 180) % 360, Saturation, Value);
                break;
            case ColorHarmonyMode.Triad:
                yield return ColorMath.FromHsv((Hue + 180 + HarmonyAngle) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + 180 - HarmonyAngle + 360) % 360, Saturation, Value);
                break;
            case ColorHarmonyMode.Analogous:
                yield return ColorMath.FromHsv((Hue - HarmonyAngle + 360) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + HarmonyAngle) % 360, Saturation, Value);
                break;
            case ColorHarmonyMode.AnalogousAccent:
                yield return ColorMath.FromHsv((Hue - HarmonyAngle + 360) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + 180) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + HarmonyAngle) % 360, Saturation, Value);
                break;
            case ColorHarmonyMode.Tetrad:
                yield return ColorMath.FromHsv((Hue + 180 + HarmonyAngle) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + 180) % 360, Saturation, Value);
                yield return ColorMath.FromHsv((Hue + HarmonyAngle) % 360, Saturation, Value);
                break;
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);

        if (HitTestMarkers(point, out bool isPrimary, out double markerHue, out bool isMovable))
        {
            if (isMovable)
            {
                _draggingMarker = true;
                _initialHarmonyAngle = HarmonyAngle;
                _draggedMarkerType = GetMarkerType(markerHue);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            return;
        }

        e.Pointer.Capture(this);
        UpdateFromPoint(point, true);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_draggingMarker)
        {
            UpdateMarkerDrag(e.GetPosition(this));
        }
        else if (_dragHue || _dragSv)
        {
            UpdateFromPoint(e.GetPosition(this), false);
        }
    }

    // Колесо мыши над кругом - быстрая смена тона: по 1°, с Shift - по 10°
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (delta == 0)
            return;

        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        var hue = (Hue + (delta > 0 ? step : -step)) % 360;
        if (hue < 0) hue += 360;
        Hue = hue;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _draggingMarker = false;
        _dragHue = false;
        _dragSv = false;
        e.Pointer.Capture(null);
    }

    private MarkerType GetMarkerType(double markerHue)
    {
        double baseHue = Hue;
        double angle = HarmonyAngle;

        switch (HarmonyMode)
        {
            case ColorHarmonyMode.Analogous:
                // Маркеры: base+angle и base-angle
                if (Math.Abs(NormalizeAngle(markerHue - baseHue) - angle) < 1)
                    return MarkerType.AnglePositive;
                else
                    return MarkerType.AngleNegative;

            case ColorHarmonyMode.AnalogousAccent:
                // Маркеры: base+angle и base-angle (base+180 неподвижный)
                if (Math.Abs(NormalizeAngle(markerHue - baseHue) - angle) < 1)
                    return MarkerType.AnglePositive;
                else
                    return MarkerType.AngleNegative;

            case ColorHarmonyMode.Triad:
                // Маркеры: base+180+angle и base+180-angle
                double offset = NormalizeAngle(markerHue - baseHue);
                if (Math.Abs(offset - (180 + angle)) < 1 || Math.Abs(offset - (180 + angle - 360)) < 1)
                    return MarkerType.AnglePositive; // соответствует 180+angle
                else
                    return MarkerType.AngleNegative; // соответствует 180-angle

            case ColorHarmonyMode.Tetrad:
                // Маркеры: base+angle (AnglePositive) и base+180+angle (OppositeAngle)
                offset = NormalizeAngle(markerHue - baseHue);
                if (Math.Abs(offset - angle) < 1)
                    return MarkerType.AnglePositive;
                else
                    return MarkerType.OppositeAngle;

            default:
                return MarkerType.AnglePositive;
        }
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;
        return angle;
    }

    private void UpdateMarkerDrag(Point point)
    {
        var bounds = new Rect(Bounds.Size);
        var center = bounds.Center;

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var markerHue = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
        if (markerHue < 0) markerHue += 360;

        double newAngle = _initialHarmonyAngle;
        double offset = NormalizeAngle(markerHue - Hue);

        switch (HarmonyMode)
        {
            case ColorHarmonyMode.Analogous:
            case ColorHarmonyMode.AnalogousAccent:
                if (_draggedMarkerType == MarkerType.AnglePositive)
                    newAngle = offset;
                else
                    newAngle = 360 - offset;
                newAngle = Math.Clamp(newAngle, 5, 90);
                break;

            case ColorHarmonyMode.Triad:
                if (_draggedMarkerType == MarkerType.AnglePositive)
                    // Маркер на base+180+angle -> angle = offset - 180
                    newAngle = offset - 180;
                else
                    // Маркер на base+180-angle -> angle = 180 - offset
                    newAngle = 180 - offset;
                newAngle = Math.Clamp(newAngle, 5, 90);
                break;

            case ColorHarmonyMode.Tetrad:
                if (_draggedMarkerType == MarkerType.AnglePositive)
                    newAngle = offset;
                else if (_draggedMarkerType == MarkerType.OppositeAngle)
                    newAngle = offset - 180;
                newAngle = Math.Clamp(newAngle, 5, 90);
                break;
        }

        if (newAngle >= 5 && newAngle <= 90)
            HarmonyAngle = newAngle;

        InvalidateVisual();
    }

    private void UpdateFromPoint(Point point, bool pickDragTarget)
    {
        var bounds = new Rect(Bounds.Size);
        var center = bounds.Center;
        var outerRadius = Math.Min(bounds.Width, bounds.Height) * 0.5 - 20;
        var ringThickness = 20.0;
        var innerRadius = outerRadius - ringThickness;
        var squareHalf = innerRadius * 0.68;
        var squareRect = new Rect(center.X - squareHalf, center.Y - squareHalf, squareHalf * 2, squareHalf * 2);

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        var isInRing = dist <= outerRadius && dist >= innerRadius;
        var isInSquare = squareRect.Contains(point);

        if (pickDragTarget)
        {
            _dragHue = isInRing;
            _dragSv = !_dragHue && isInSquare;
        }

        if (_dragHue)
        {
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
            if (angle < 0)
                angle += 360;
            Hue = angle;
            InvalidateVisual();
        }
        else if (_dragSv)
        {
            var s = (point.X - squareRect.Left) / squareRect.Width;
            var v = 1 - (point.Y - squareRect.Top) / squareRect.Height;
            Saturation = ColorMath.Clamp01(s);
            Value = ColorMath.Clamp01(v);
            InvalidateVisual();
        }
    }

    private bool HitTestMarkers(Point point, out bool isPrimary, out double markerHue, out bool isMovable)
    {
        isPrimary = false;
        markerHue = 0;
        isMovable = false;

        var bounds = new Rect(Bounds.Size);
        var center = bounds.Center;
        var outerRadius = Math.Min(bounds.Width, bounds.Height) * 0.5 - 20;
        const double hitTolerance = 8;

        var harmonyHues = GetHarmonyHues(Hue, HarmonyMode, HarmonyAngle);
        foreach (var h in harmonyHues)
        {
            bool primary = Math.Abs(h - Hue) < 0.1;
            bool movable = IsMarkerMovable(primary, h);
            double markerRadius = movable ? 6 : 4;
            double actualHitTolerance = hitTolerance + markerRadius;

            var radians = (h - 90) * Math.PI / 180.0;
            var markerPos = new Point(
                center.X + Math.Cos(radians) * (outerRadius + 8),
                center.Y + Math.Sin(radians) * (outerRadius + 8));

            if (Math.Abs(point.X - markerPos.X) <= actualHitTolerance && Math.Abs(point.Y - markerPos.Y) <= actualHitTolerance)
            {
                markerHue = h;
                isPrimary = primary;
                isMovable = movable;
                return true;
            }
        }
        return false;
    }

    private bool IsMarkerMovable(bool isPrimary, double hue)
    {
        if (isPrimary) return false;

        switch (HarmonyMode)
        {
            case ColorHarmonyMode.Complementary:
                return false;

            case ColorHarmonyMode.Tetrad:
                double diffTetrad = Math.Abs((hue - Hue + 360) % 360);
                if (diffTetrad > 179 && diffTetrad < 181)
                    return false;
                return true;

            case ColorHarmonyMode.AnalogousAccent:
                double diffAccent = Math.Abs((hue - Hue + 360) % 360);
                if (diffAccent > 179 && diffAccent < 181)
                    return false;
                return true;

            default:
                return true;
        }
    }

    private void DrawHueRing(DrawingContext context, Point center, double outerRadius, double thickness)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var size = Bounds.Size;
        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width * scaling)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));

        if (_hueRingBitmap is null || _hueRingPixelSize != pixelSize || _hueRingScaling != scaling)
        {
            _hueRingBitmap?.Dispose();
            _hueRingBitmap = CreateHueRingBitmap(pixelSize, scaling, center, outerRadius, thickness);
            _hueRingPixelSize = pixelSize;
            _hueRingScaling = scaling;
        }

        context.DrawImage(_hueRingBitmap, new Rect(0, 0, pixelSize.Width / scaling, pixelSize.Height / scaling));
    }

    private static unsafe WriteableBitmap CreateHueRingBitmap(PixelSize pixelSize, double scaling, Point centerDip, double outerRadiusDip, double thicknessDip)
    {
        var bitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Bgra8888);

        var cx = centerDip.X * scaling;
        var cy = centerDip.Y * scaling;
        var outer = outerRadiusDip * scaling;
        var inner = (outerRadiusDip - thicknessDip) * scaling;

        using var frame = bitmap.Lock();
        var basePtr = (byte*)frame.Address;
        for (var y = 0; y < pixelSize.Height; y++)
        {
            var row = (uint*)(basePtr + y * frame.RowBytes);
            var dy = y + 0.5 - cy;
            for (var x = 0; x < pixelSize.Width; x++)
            {
                var dx = x + 0.5 - cx;
                var distance = Math.Sqrt(dx * dx + dy * dy);

                // Сглаживание краёв кольца: доля покрытия пикселя
                var coverage = Math.Clamp(outer - distance + 0.5, 0, 1) * Math.Clamp(distance - inner + 0.5, 0, 1);
                if (coverage <= 0)
                {
                    row[x] = 0;
                    continue;
                }

                // Та же формула угла, что и в UpdateFromPoint: 0° сверху, по часовой стрелке
                var hue = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
                if (hue < 0) hue += 360;
                var color = ColorMath.FromHsv(hue, 1, 1);

                // Премультиплицированный BGRA
                var a = (uint)Math.Round(coverage * 255);
                var r = color.R * a / 255;
                var g = color.G * a / 255;
                var b = color.B * a / 255;
                row[x] = (a << 24) | (r << 16) | (g << 8) | b;
            }
        }

        return bitmap;
    }

    private void DrawHueMarker(DrawingContext context, Point center, double outerRadius, double hue, bool isPrimary, bool isMovable)
    {
        var hueRadians = (hue - 90) * Math.PI / 180.0;
        var markerRadiusOffset = outerRadius + 8;
        var point = new Point(
            center.X + Math.Cos(hueRadians) * markerRadiusOffset,
            center.Y + Math.Sin(hueRadians) * markerRadiusOffset);

        double radius = isMovable ? 6 : 4;
        IBrush fill;
        IPen pen;

        if (isMovable)
        {
            fill = MovableMarkerFill;
            pen = WhiteMarkerPen;
        }
        else if (isPrimary)
        {
            fill = PrimaryMarkerFill;
            pen = WhiteMarkerPen;
        }
        else
        {
            fill = FixedMarkerFill;
            pen = FixedMarkerPen;
        }

        context.DrawEllipse(fill, pen, point, radius, radius);
    }

    private void DrawHarmonyMarkers(DrawingContext context, Point center, double outerRadius)
    {
        var harmonyHues = GetHarmonyHues(Hue, HarmonyMode, HarmonyAngle);
        foreach (var h in harmonyHues)
        {
            bool isPrimary = Math.Abs(h - Hue) < 0.1;
            if (isPrimary) continue;
            bool isMovable = IsMarkerMovable(isPrimary, h);
            DrawHueMarker(context, center, outerRadius, h, false, isMovable);
        }
    }

    private static IEnumerable<double> GetHarmonyHues(double baseHue, ColorHarmonyMode mode, double angle)
    {
        switch (mode)
        {
            case ColorHarmonyMode.Single:
                yield return baseHue;
                break;
            case ColorHarmonyMode.Complementary:
                yield return (baseHue + 180) % 360;
                break;
            case ColorHarmonyMode.Triad:
                yield return (baseHue + 180 + angle) % 360;
                yield return (baseHue + 180 - angle + 360) % 360;
                break;
            case ColorHarmonyMode.Analogous:
                yield return (baseHue + angle) % 360;
                yield return (baseHue - angle + 360) % 360;
                break;
            case ColorHarmonyMode.AnalogousAccent:
                yield return (baseHue + angle) % 360;
                yield return (baseHue - angle + 360) % 360;
                yield return (baseHue + 180) % 360;
                break;
            case ColorHarmonyMode.Tetrad:
                yield return (baseHue + angle) % 360;
                yield return (baseHue + 180) % 360;
                yield return (baseHue + 180 + angle) % 360;
                break;
        }
    }

    private void DrawSvSquare(DrawingContext context, Rect rect)
    {
        var hueColor = ColorMath.FromHsv(Hue, 1, 1);
        context.DrawRectangle(new ImmutableSolidColorBrush(hueColor), null, rect);
        context.DrawRectangle(SvWhiteGradient, null, rect);
        context.DrawRectangle(SvBlackGradient, null, rect);
        context.DrawRectangle(null, SvBorderPen, rect);
    }
}
