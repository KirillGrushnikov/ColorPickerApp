using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ColorPickerApp.Views;

/// <summary>
/// Полноэкранная пипетка поверх снимка всех мониторов.
/// ЛКМ / Enter / пробел - выбрать; ПКМ / Esc - отмена; колесо, + и - - масштаб лупы;
/// стрелки - сдвиг на 1 пиксель (с Shift - на 10).
/// </summary>
public sealed class EyedropperOverlayWindow : Window
{
    private readonly ScreenCapture _capture;
    private readonly WriteableBitmap _screenBitmap;
    private readonly OverlayControl _overlay;

    private Color? _result;
    private int _pixelX = -1;
    private int _pixelY = -1;

    private EyedropperOverlayWindow(ScreenCapture capture, double renderScaling)
    {
        _capture = capture;
        var scaling = Math.Max(0.1, renderScaling);

        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowState = WindowState.Normal;

        Position = new PixelPoint(capture.X, capture.Y);
        Width = capture.Width / scaling;
        Height = capture.Height / scaling;
        Cursor = new Cursor(StandardCursorType.Cross);
        Background = Brushes.Transparent;

        _screenBitmap = capture.ToBitmap();
        _overlay = new OverlayControl(capture, App.Settings.EyedropperZoomIndex);

        Content = new Grid
        {
            Children =
            {
                new Image { Stretch = Stretch.Fill, Source = _screenBitmap },
                _overlay,
            },
        };

        // Tunnel: стрелки и Enter получаем раньше, чем их заберёт навигация фокусом
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Делает снимок экранов, показывает оверлей и ждёт выбора. null - отмена.</summary>
    public static async Task<Color?> PickAsync(Window owner)
    {
        var capture = ScreenCapture.CaptureAllScreens(owner, out var scaling);
        var window = new EyedropperOverlayWindow(capture, scaling);

        var tcs = new TaskCompletionSource<Color?>();
        window.Closed += (_, _) => tcs.TrySetResult(window._result);
        window.Show();

        var result = await tcs.Task;
        await Task.Delay(50);
        return result;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Activate(); // чтобы клавиатура работала сразу

        // Лупа сразу у курсора (а не в углу экрана до первого движения мыши)
        Dispatcher.UIThread.Post(InitFromCursor, DispatcherPriority.Loaded);
    }

    private void InitFromCursor()
    {
        if (_pixelX >= 0 || GetCursorPos(out var cursor) == 0)
            return;

        _pixelX = Math.Clamp(cursor.X - _capture.X, 0, _capture.Width - 1);
        _pixelY = Math.Clamp(cursor.Y - _capture.Y, 0, _capture.Height - 1);
        var (dipX, dipY) = ToDip(_pixelX, _pixelY);
        _overlay.UpdateCenter(_pixelX, _pixelY, dipX, dipY);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _screenBitmap.Dispose();
        App.Settings.EyedropperZoomIndex = _overlay.ZoomIndex;
        App.Settings.Save();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        (_pixelX, _pixelY) = ToCapturePixel(point);
        _overlay.UpdateCenter(_pixelX, _pixelY, point.X, point.Y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            Cancel();
        }
        else if (props.IsLeftButtonPressed)
        {
            var (x, y) = ToCapturePixel(e.GetPosition(this));
            Pick(x, y);
        }

        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y != 0)
            _overlay.Zoom(e.Delta.Y > 0 ? +1 : -1);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Escape:
                Cancel();
                break;
            case Key.Enter:
            case Key.Space:
                if (_pixelX >= 0)
                    Pick(_pixelX, _pixelY);
                break;
            case Key.Left:
                MoveBy(-step, 0);
                break;
            case Key.Right:
                MoveBy(step, 0);
                break;
            case Key.Up:
                MoveBy(0, -step);
                break;
            case Key.Down:
                MoveBy(0, step);
                break;
            case Key.OemPlus:
            case Key.Add:
                _overlay.Zoom(+1);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                _overlay.Zoom(-1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Pick(int x, int y)
    {
        _result = _capture.GetPixel(x, y);
        Close();
    }

    private void Cancel()
    {
        _result = null;
        Close();
    }

    /// <summary>Сдвигает курсор ровно на N пикселей экрана (а не DIP) - для точного попадания.</summary>
    private void MoveBy(int dx, int dy)
    {
        if (_pixelX < 0)
        {
            // Курсор ещё не двигали - берём центр
            _pixelX = _capture.Width / 2;
            _pixelY = _capture.Height / 2;
        }

        _pixelX = Math.Clamp(_pixelX + dx, 0, _capture.Width - 1);
        _pixelY = Math.Clamp(_pixelY + dy, 0, _capture.Height - 1);
        SetCursorPos(_capture.X + _pixelX, _capture.Y + _pixelY);

        var (dipX, dipY) = ToDip(_pixelX, _pixelY);
        _overlay.UpdateCenter(_pixelX, _pixelY, dipX, dipY);
    }

    // DIP окна -> пиксель снимка
    private (int X, int Y) ToCapturePixel(Point pointDip)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
            return (0, 0);

        // +1e-4: после SetCursorPos координата в DIP может оказаться чуть меньше целого пикселя
        var x = (int)Math.Floor(pointDip.X * _capture.Width / width + 1e-4);
        var y = (int)Math.Floor(pointDip.Y * _capture.Height / height + 1e-4);
        return (Math.Clamp(x, 0, _capture.Width - 1), Math.Clamp(y, 0, _capture.Height - 1));
    }

    private (double X, double Y) ToDip(int pixelX, int pixelY)
    {
        var scaleX = Bounds.Width > 0 ? Bounds.Width / _capture.Width : 1;
        var scaleY = Bounds.Height > 0 ? Bounds.Height / _capture.Height : 1;
        return (pixelX * scaleX, pixelY * scaleY);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern int SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetCursorPos(out POINT point);

    private sealed class OverlayControl : Control
    {
        // Размер одной ячейки лупы в DIP; лупа всегда около 150 DIP, меняется число ячеек
        private static readonly int[] ZoomLevels = { 3, 5, 7, 10, 15, 21, 30 };
        private const double TargetLoupeSize = 150;
        private const double FrameOffset = 3;

        private static readonly IPen OuterFramePen = new ImmutablePen(Brushes.Black, 4);
        private static readonly IPen InnerFramePen = new ImmutablePen(Brushes.White, 2);
        private static readonly IPen CenterOuterPen = new ImmutablePen(Brushes.White, 2);
        private static readonly IPen CenterInnerPen = new ImmutablePen(Brushes.Black, 1);
        private static readonly Typeface TextTypeface = new("Segoe UI");
        private static readonly Typeface HintTypeface = new("Segoe UI");

        private readonly ScreenCapture _capture;
        private WriteableBitmap? _loupe;
        private int _loupeCells;

        private int _centerX, _centerY;
        private double _mouseDipX, _mouseDipY;

        public OverlayControl(ScreenCapture capture, int zoomIndex)
        {
            _capture = capture;
            ZoomIndex = Math.Clamp(zoomIndex, 0, ZoomLevels.Length - 1);
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        public int ZoomIndex { get; private set; }

        private int CellSize => ZoomLevels[ZoomIndex];

        /// <summary>Нечётное число ячеек на сторону, чтобы был центральный пиксель.</summary>
        private int CellsPerSide
        {
            get
            {
                var cells = (int)Math.Round(TargetLoupeSize / CellSize);
                if (cells % 2 == 0) cells++;
                return Math.Max(5, cells);
            }
        }

        public void Zoom(int direction)
        {
            var index = Math.Clamp(ZoomIndex + direction, 0, ZoomLevels.Length - 1);
            if (index == ZoomIndex)
                return;
            ZoomIndex = index;
            InvalidateVisual();
        }

        public void UpdateCenter(int centerX, int centerY, double mouseDipX, double mouseDipY)
        {
            _centerX = centerX;
            _centerY = centerY;
            _mouseDipX = mouseDipX;
            _mouseDipY = mouseDipY;
            InvalidateVisual();
        }

        private static Color GetContrastColor(Color c)
        {
            double brightness = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return brightness > 128 ? Colors.Black : Colors.White;
        }

        private Color GetAverageColor(int centerX, int centerY, int radius)
        {
            int r = 0, g = 0, b = 0, count = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var c = _capture.GetPixel(centerX + dx, centerY + dy);
                    r += c.R;
                    g += c.G;
                    b += c.B;
                    count++;
                }
            }
            return Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
        }

        private unsafe void FillLoupe(int cells)
        {
            if (_loupe is null || _loupeCells != cells)
            {
                _loupe?.Dispose();
                _loupe = new WriteableBitmap(new PixelSize(cells, cells), new Vector(96, 96), PixelFormat.Bgra8888);
                _loupeCells = cells;
            }

            var half = cells / 2;
            using var frame = _loupe.Lock();
            var basePtr = (byte*)frame.Address;
            for (int row = 0; row < cells; row++)
            {
                var dst = (uint*)(basePtr + row * frame.RowBytes);
                int sy = _centerY + row - half;
                for (int col = 0; col < cells; col++)
                    dst[col] = _capture.GetArgb(_centerX + col - half, sy); // BGRA в памяти = ARGB в uint
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var cells = CellsPerSide;
            var cellSize = CellSize;
            var loupeSize = (double)cells * cellSize;
            var half = cells / 2;
            var centerColor = _capture.GetPixel(_centerX, _centerY);

            // Позиционирование лупы (чтобы не вылезала за границы окна)
            double boxX = _mouseDipX + 20;
            double boxY = _mouseDipY + 20;
            if (boxX + loupeSize > Bounds.Width) boxX = _mouseDipX - loupeSize - 20;
            if (boxY + loupeSize + 44 > Bounds.Height) boxY = _mouseDipY - loupeSize - 64;

            // 1. Увеличенная область
            FillLoupe(cells);
            context.DrawImage(_loupe!, new Rect(boxX, boxY, loupeSize, loupeSize));

            // 2. Рамка
            var borderRect = new Rect(boxX - FrameOffset, boxY - FrameOffset, loupeSize + 2 * FrameOffset, loupeSize + 2 * FrameOffset);
            context.DrawRectangle(null, OuterFramePen, borderRect);
            context.DrawRectangle(null, InnerFramePen, borderRect);

            // 3. Центральный пиксель
            var centerRect = new Rect(boxX + half * cellSize, boxY + half * cellSize, cellSize, cellSize);
            context.DrawRectangle(null, CenterOuterPen, centerRect.Inflate(1));
            context.DrawRectangle(null, CenterInnerPen, centerRect);

            // 4. Подпись: hex и увеличение; фон контрастен экрану под левым нижним углом лупы
            var physicalPerDip = Bounds.Width > 0 ? _capture.Width / Bounds.Width : 1;
            var scaleY = Bounds.Height > 0 ? _capture.Height / Bounds.Height : 1;
            var underText = GetAverageColor(
                (int)((boxX - FrameOffset) * physicalPerDip),
                (int)((boxY + loupeSize + FrameOffset) * scaleY),
                radius: 1);
            var textBgColor = GetContrastColor(underText);
            var textFgColor = GetContrastColor(textBgColor);
            var textBrush = new ImmutableSolidColorBrush(textFgColor);

            // Во сколько раз один пиксель экрана больше своего реального размера
            var magnification = cellSize * physicalPerDip;
            var caption = string.Format(CultureInfo.InvariantCulture, "{0}   ×{1:0}",
                ColorFormat.Hex(centerColor), magnification);

            var text = new FormattedText(caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                TextTypeface, 14, textBrush);
            var hint = new FormattedText(Loc.T("eyedropper_hint"),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, HintTypeface, 11, textBrush);

            var textPosition = new Point(boxX, boxY + loupeSize + 8);
            var hintPosition = new Point(boxX, textPosition.Y + text.Height + 2);
            var backgroundRect = new Rect(
                textPosition.X - 3,
                textPosition.Y - 2,
                Math.Max(text.Width, hint.Width) + 6,
                text.Height + hint.Height + 6);

            context.DrawRectangle(new ImmutableSolidColorBrush(textBgColor), null, backgroundRect);
            context.DrawText(text, textPosition);
            context.DrawText(hint, hintPosition);
        }
    }
}
