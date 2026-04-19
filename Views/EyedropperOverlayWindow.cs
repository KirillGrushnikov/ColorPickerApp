using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Splat.ModeDetection;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using AImage = Avalonia.Controls.Image;
using APoint = Avalonia.Point;
using DrawingBitmap = System.Drawing.Bitmap;

namespace ColorPickerApp.Views;

public class EyedropperOverlayWindow : Window
{
    private readonly DrawingBitmap _capture;
    private readonly PixelRect _virtualBounds;
    private readonly double _renderScaling;
    private readonly AImage _image;
    private readonly OverlayControl _overlay;

    private Avalonia.Media.Color? _result;

    public EyedropperOverlayWindow(DrawingBitmap capture, PixelRect virtualBounds, double renderScaling)
    {
        _capture = capture;
        _virtualBounds = virtualBounds;
        _renderScaling = Math.Max(0.1, renderScaling);

        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowState = WindowState.Normal;
        

        Position = new PixelPoint(virtualBounds.X, virtualBounds.Y);

        Width = virtualBounds.Width / _renderScaling;
        Height = virtualBounds.Height / _renderScaling;
        Cursor = new Cursor(StandardCursorType.Cross);
        Background = Brushes.Transparent;

        _image = new AImage
        {
            Stretch = Stretch.Fill,
            Source = ToAvaloniaBitmap(capture),
        };


        _overlay = new OverlayControl(() => _lastPoint, GetPixelColorAt);

        Content = new Grid
        {
            Children =
            {
                _image,
                _overlay,
            },
        };
    }

    

    private APoint _lastPoint;

    public Avalonia.Media.Color? Result => _result;

    private int _lastCaptureX = -1, _lastCaptureY = -1;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _lastPoint = e.GetPosition(this);
        var (captureX, captureY) = GetCaptureCoords(_lastPoint);


        _overlay.UpdateCenter(captureX, captureY, _lastPoint.X, _lastPoint.Y, this);
        _overlay.InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pointDip = e.GetPosition(this);
        var (x, y) = GetCaptureCoords(pointDip);
        _result = GetPixelColorAt(new Point(x, y));
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _result = null;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    private (int x, int y) GetCaptureCoords(APoint pointDip)
    {
        // pointDip.X/Y в DIP, Bounds.Width/Height в DIP, _capture.Width/Height в пикселях
        double scaleX = _capture.Width / Bounds.Width;
        double scaleY = _capture.Height / Bounds.Height;
        int x = (int)Math.Clamp(pointDip.X * scaleX, 0, _capture.Width - 1);
        int y = (int)Math.Clamp(pointDip.Y * scaleY, 0, _capture.Height - 1);
        return (x, y);
    }

    private Avalonia.Media.Color GetPixelColorAt(APoint point)
    {
        var x = (int)Math.Clamp(point.X, 0, _capture.Width - 1);
        var y = (int)Math.Clamp(point.Y, 0, _capture.Height - 1);
        var c = _capture.GetPixel(x, y);
        return Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    public static (DrawingBitmap Bitmap, PixelRect Bounds, double RenderScaling) CaptureAllScreens(Window owner)
    {
        var screens = owner.Screens?.All;
        if (screens == null || screens.Count == 0)
            throw new InvalidOperationException("No screens available.");

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxRenderScaling = owner.RenderScaling;
        foreach (var screen in screens)
        {
            var b = screen.Bounds;
            if (b.X < minX) minX = b.X;
            if (b.Y < minY) minY = b.Y;
            if (b.Right > maxX) maxX = b.Right;
            if (b.Bottom > maxY) maxY = b.Bottom;
            if(screen.Scaling > maxRenderScaling) maxRenderScaling = screen.Scaling;
        }

        var width = maxX - minX;
        var height = maxY - minY;
        var bitmap = new DrawingBitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(minX, minY, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);
        return (bitmap, new PixelRect(minX, minY, width, height), maxRenderScaling);
    }

    private static Avalonia.Media.Imaging.Bitmap ToAvaloniaBitmap(DrawingBitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        return new Avalonia.Media.Imaging.Bitmap(ms);
    }

    private class OverlayControl : Control
    {
        public OverlayControl(
            Func<APoint> currentPointProvider,
            Func<APoint, Avalonia.Media.Color> colorAt) : base()
        {
            _currentPointProvider = currentPointProvider;
            _colorAt = colorAt;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        Func<APoint> _currentPointProvider;
        Func<APoint, Avalonia.Media.Color> _colorAt;

        private int _centerX, _centerY;
        private double _mouseDipX, _mouseDipY;

        private const int SamplesPerEdge = 50; // количество сегментов на сторону рамки

        private Avalonia.Media.Color GetAverageColor(APoint center, int radius = 1)
        {
            int r = 0, g = 0, b = 0, a = 0;
            int count = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var c = _colorAt(new APoint(center.X + dx, center.Y + dy));
                    r += c.R;
                    g += c.G;
                    b += c.B;
                    a += c.A;
                    count++;
                }
            }
            if (count == 0) return Colors.Black;
            return Avalonia.Media.Color.FromArgb(
                (byte)(a / count),
                (byte)(r / count),
                (byte)(g / count),
                (byte)(b / count));
        }

        public void UpdateCenter(int centerX, int centerY, double mouseDipX, double mouseDipY, Control? owner = null)
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


        private WriteableBitmap? _cachedBitmap;
        private int _cachedSize = -1;

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var p = new Point(_centerX, _centerY);
            var centerColor = _colorAt(p);
            var contrastCenter = GetContrastColor(centerColor);

            const int sample = 7;                // радиус в пикселях исходного изображения
            const int cellSizePx = 10;           // размер одной ячейки в увеличенной лупе (пикселей на экране)
            int cellsPerSide = sample * 2 + 1;   // 15
            double actualSize = cellsPerSide * cellSizePx; // 150 пикселей

            // Позиционирование лупы (чтобы не вылезала за границы окна)
            double boxX = _mouseDipX + 20;
            double boxY = _mouseDipY + 20;
            if (boxX + actualSize > Bounds.Width) boxX = _mouseDipX - actualSize - 20;
            if (boxY + actualSize > Bounds.Height) boxY = _mouseDipY - actualSize - 20;

            double boxXM = _centerX;
            double boxYM = _centerY;
            if (boxX + actualSize > Bounds.Width) boxXM = _centerX - actualSize;
            if (boxY + actualSize > Bounds.Height) boxYM = _centerY - actualSize;

            // ---- 1. Отрисовка увеличенной области через WriteableBitmap (без зазоров) ----
            // Пересоздаём битмап, только если изменился размер (не обязательно, но для производительности)
            if (_cachedBitmap == null || _cachedSize != cellsPerSide)
            {
                _cachedBitmap = new WriteableBitmap(new PixelSize(cellsPerSide, cellsPerSide), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);
                _cachedSize = cellsPerSide;
            }

            using (var fb = _cachedBitmap.Lock())
            {
                unsafe
                {
                    var ptr = (byte*)fb.Address;
                    int stride = fb.RowBytes;
                    for (int y = -sample; y <= sample; y++)
                    {
                        byte* row = ptr + (y + sample) * stride;
                        for (int x = -sample; x <= sample; x++)
                        {
                            var c = _colorAt(new APoint(_centerX + x, _centerY + y));
                            int idx = (x + sample) * 4;
                            row[idx] = c.B;     // BGRA порядок
                            row[idx + 1] = c.G;
                            row[idx + 2] = c.R;
                            row[idx + 3] = c.A;
                        }
                    }
                }
            }

            context.DrawImage(_cachedBitmap, new Rect(boxX, boxY, actualSize, actualSize));

            // Углы рамки
            double offset = 3;
            var borderRect = new Rect(boxX - offset, boxY - offset, actualSize + 2 * offset, actualSize + 2 * offset);
            context.DrawRectangle(null, new Pen(Brushes.Black, 4), borderRect);
            context.DrawRectangle(null, new Pen(Brushes.White, 2), borderRect);


            // Контрастный цвет левого нижнего угла (для фона текста)
            var bottomLeft = new APoint(boxX - offset, boxY + actualSize + offset);
            var avgBL = GetAverageColor(bottomLeft, radius: 1);
            var contrastBL = GetContrastColor(avgBL);

            // Фон текста и цвет текста на основе левого нижнего угла
            var textBgColor = contrastBL;
            var textFgColor = GetContrastColor(textBgColor);

            // Центральный квадратик
            var centerRect = new Rect(boxX + sample * cellSizePx, boxY + sample * cellSizePx, cellSizePx, cellSizePx);
            context.DrawRectangle(null, new Pen(Brushes.White, 2), centerRect);
            context.DrawRectangle(null, new Pen(Brushes.Black, 1),
                new Rect(centerRect.X + 1, centerRect.Y + 1, centerRect.Width - 2, centerRect.Height - 2));

            // Текст с фоном от левого нижнего угла
            string hex = $"#{centerColor.R:X2}{centerColor.G:X2}{centerColor.B:X2}";
            var text = new FormattedText(
                hex,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                14,
                new SolidColorBrush(textFgColor));

            var textPosition = new APoint(boxX, boxY + actualSize + 8);
            double textWidth = text.Width;
            double textHeight = text.Height;
            var backgroundRect = new Rect(textPosition.X - 2, textPosition.Y - 2, textWidth + 4, textHeight + 4);

            context.DrawRectangle(new SolidColorBrush(textBgColor), null, backgroundRect);
            context.DrawText(text, textPosition);
        }
    }
}
