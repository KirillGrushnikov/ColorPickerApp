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

public enum OklchChannel
{
    Lightness,
    Chroma,
    Hue,
}

/// <summary>Общие помощники отрисовки шкал OKLCH.</summary>
internal static class OklchPaint
{
    private static readonly Color OutOfGamutTint = Color.FromRgb(0x2B, 0x2B, 0x2B);

    /// <summary>
    /// Пиксель шкалы в премультиплицированном BGRA. Цвета вне sRGB затемняются -
    /// так видно, где кончаются цвета, которые экран может показать.
    /// </summary>
    public static uint ToPixel(double lightness01, double chroma, double hue, byte alpha = 255)
    {
        var color = ColorMath.OklchToDisplay(lightness01, chroma, hue, out var inGamut);
        if (!inGamut)
            color = Blend(color, OutOfGamutTint, 0.6);

        uint a = alpha;
        uint r = color.R * a / 255;
        uint g = color.G * a / 255;
        uint b = color.B * a / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    private static Color Blend(Color c, Color with, double amount) => Color.FromRgb(
        (byte)Math.Round(c.R + (with.R - c.R) * amount),
        (byte)Math.Round(c.G + (with.G - c.G) * amount),
        (byte)Math.Round(c.B + (with.B - c.B) * amount));
}

/// <summary>
/// Ползунок одного канала OKLCH. Шкала показывает, как изменится цвет при сдвиге ползунка
/// (с учётом двух других каналов). Перетаскивание, клик, колесо мыши и стрелки (Shift - крупный шаг).
/// </summary>
public class OklchSlider : Control
{
    public static readonly StyledProperty<OklchChannel> ChannelProperty =
        AvaloniaProperty.Register<OklchSlider, OklchChannel>(nameof(Channel));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<OklchSlider, double>(nameof(Value));

    /// <summary>Светлота в процентах 0..100.</summary>
    public static readonly StyledProperty<double> LightnessProperty =
        AvaloniaProperty.Register<OklchSlider, double>(nameof(Lightness), 50);

    public static readonly StyledProperty<double> ChromaProperty =
        AvaloniaProperty.Register<OklchSlider, double>(nameof(Chroma));

    public static readonly StyledProperty<double> HueAngleProperty =
        AvaloniaProperty.Register<OklchSlider, double>(nameof(HueAngle));

    private const double ThumbRadius = 8;
    private const double TrackHeight = 12;

    private static readonly IPen TrackBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
    private static readonly IPen ThumbOuterPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
    private static readonly IPen ThumbInnerPen = new ImmutablePen(Brushes.White, 2);
    private static readonly IPen FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);

    private WriteableBitmap? _gradient;
    private (int Width, OklchChannel Channel, double A, double B) _gradientKey;
    private bool _dragging;
    private bool _showFocus;

    static OklchSlider()
    {
        AffectsRender<OklchSlider>(ChannelProperty, ValueProperty, LightnessProperty, ChromaProperty, HueAngleProperty);
        FocusableProperty.OverrideDefaultValue<OklchSlider>(true);
    }

    public OklchSlider()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        MinHeight = 22;
    }

    public OklchChannel Channel
    {
        get => GetValue(ChannelProperty);
        set => SetValue(ChannelProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
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

    private (double Min, double Max) Range => Channel switch
    {
        OklchChannel.Lightness => (0, 100),
        OklchChannel.Chroma => (0, 0.37),
        _ => (0, 359.9),
    };

    private (double Small, double Large) Steps => Channel switch
    {
        OklchChannel.Lightness => (0.5, 5),
        OklchChannel.Chroma => (0.002, 0.02),
        _ => (1, 10),
    };

    private Rect TrackRect
    {
        get
        {
            var b = Bounds;
            return new Rect(ThumbRadius, (b.Height - TrackHeight) / 2, Math.Max(1, b.Width - 2 * ThumbRadius), TrackHeight);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 2 * ThumbRadius)
            return;

        var track = TrackRect;
        EnsureGradient(track.Width);

        var radius = TrackHeight / 2;
        using (context.PushClip(new RoundedRect(track, radius)))
            context.DrawImage(_gradient!, track);
        context.DrawRectangle(null, TrackBorderPen, track, radius, radius);

        // Ползунок: залит текущим цветом
        var (min, max) = Range;
        var t = Math.Clamp((Value - min) / (max - min), 0, 1);
        var center = new Point(track.X + t * track.Width, Bounds.Height / 2);
        var fill = new ImmutableSolidColorBrush(ColorMath.FromOklch(Lightness / 100, Chroma, HueAngle));

        context.DrawEllipse(fill, ThumbOuterPen, center, ThumbRadius, ThumbRadius);
        context.DrawEllipse(null, ThumbInnerPen, center, ThumbRadius - 1.5, ThumbRadius - 1.5);
        if (_showFocus)
            context.DrawEllipse(null, FocusPen, center, ThumbRadius + 2.5, ThumbRadius + 2.5);
    }

    // Шкала - битмап высотой 1 пиксель, пересчитывается только при изменении двух других каналов или ширины
    private unsafe void EnsureGradient(double trackWidthDip)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var width = Math.Max(1, (int)Math.Ceiling(trackWidthDip * scaling));
        var channel = Channel;
        var key = channel switch
        {
            OklchChannel.Lightness => (width, channel, Chroma, HueAngle),
            OklchChannel.Chroma => (width, channel, Lightness, HueAngle),
            _ => (width, channel, Lightness, Chroma),
        };
        if (_gradient != null && key == _gradientKey)
            return;

        _gradient?.Dispose();
        _gradient = new WriteableBitmap(new PixelSize(width, 1), new Vector(96, 96), PixelFormat.Bgra8888);
        _gradientKey = key;

        var (min, max) = Range;
        using var frame = _gradient.Lock();
        var row = (uint*)frame.Address;
        for (var x = 0; x < width; x++)
        {
            var v = min + (x + 0.5) / width * (max - min);
            row[x] = channel switch
            {
                OklchChannel.Lightness => OklchPaint.ToPixel(v / 100, Chroma, HueAngle),
                OklchChannel.Chroma => OklchPaint.ToPixel(Lightness / 100, v, HueAngle),
                _ => OklchPaint.ToPixel(Lightness / 100, Chroma, v),
            };
        }
    }

    private void SetValueFromX(double x)
    {
        var track = TrackRect;
        var (min, max) = Range;
        var t = Math.Clamp((x - track.X) / track.Width, 0, 1);
        Value = min + t * (max - min);
    }

    private void Step(double delta)
    {
        var (min, max) = Range;
        var value = Value + delta;
        if (Channel == OklchChannel.Hue)
        {
            // Тон по кругу: 359 -> 0
            value %= 360;
            if (value < 0) value += 360;
            Value = Math.Min(value, max);
        }
        else
        {
            Value = Math.Clamp(value, min, max);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _dragging = true;
        e.Pointer.Capture(this);
        Focus();
        SetValueFromX(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            SetValueFromX(e.GetPosition(this).X);
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
        // С зажатым Shift Windows может прислать горизонтальную прокрутку
        var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (delta == 0)
            return;

        var (small, large) = Steps;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? large : small;
        Step(delta > 0 ? step : -step);
        e.Handled = true; // не прокручивать вкладку
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var (small, large) = Steps;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? large : small;
        var (min, max) = Range;

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
            case Key.PageDown:
                Step(-large);
                break;
            case Key.PageUp:
                Step(large);
                break;
            case Key.Home:
                Value = min;
                break;
            case Key.End:
                Value = max;
                break;
            default:
                base.OnKeyDown(e);
                return;
        }

        e.Handled = true;
    }
}
