using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

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
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            new Pen(Brushes.White, 2),
            svPoint,
            6,
            6);
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

    private static void DrawHueRing(DrawingContext context, Point center, double outerRadius, double thickness)
    {
        const int segments = 720;
        var penThickness = 2;
        for (var i = 0; i < segments; i++)
        {
            var h = i * (360.0 / segments);
            var a1 = (h - 90) * Math.PI / 180.0;
            var a2 = ((h + 360.0 / segments) - 90) * Math.PI / 180.0;
            var p1 = new Point(center.X + Math.Cos(a1) * (outerRadius - thickness), center.Y + Math.Sin(a1) * (outerRadius - thickness));
            var p2 = new Point(center.X + Math.Cos(a2) * (outerRadius), center.Y + Math.Sin(a2) * (outerRadius));
            var color = ColorMath.FromHsv(h, 1, 1);
            context.DrawLine(new Pen(new SolidColorBrush(color), penThickness), p1, p2);
        }
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
        Pen pen;

        if (isMovable)
        {
            fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            pen = new Pen(Brushes.White, 2);
        }
        else
        {
            if (isPrimary)
            {
                fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                pen = new Pen(Brushes.White, 2);
            }
            else
            {
                fill = new SolidColorBrush(Color.FromArgb(180, 30, 30, 30));
                pen = new Pen(new SolidColorBrush(Color.FromRgb(70, 70, 70)), 2);
                //var markerColor = ColorMath.FromHsv(hue, Saturation, Value);
                //pen = new Pen(new SolidColorBrush(markerColor), 2);
            }
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
        context.DrawRectangle(new SolidColorBrush(hueColor), null, rect);
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new(Colors.White, 0),
                    new(Color.FromArgb(0, 255, 255, 255), 1),
                },
            },
            null,
            rect);
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new(Color.FromArgb(0, 0, 0, 0), 0),
                    new(Colors.Black, 1),
                },
            },
            null,
            rect);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1), rect);
    }
}