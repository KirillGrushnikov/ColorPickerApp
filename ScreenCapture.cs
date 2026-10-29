using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;

namespace ColorPickerApp;

/// <summary>
/// Снимок всего виртуального экрана (все мониторы) в 32-битном BGRA.
/// Работает напрямую через GDI (BitBlt + GetDIBits): раньше использовались System.Drawing,
/// GetPixel и перекодирование всего снимка в PNG и обратно - это и было основной задержкой открытия пипетки.
/// </summary>
public sealed class ScreenCapture
{
    private readonly uint[] _pixels; // 0xAARRGGBB, построчно сверху вниз

    private ScreenCapture(int x, int y, int width, int height, uint[] pixels)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>Снимает все экраны. <paramref name="maxScaling"/> - наибольший масштаб среди мониторов.</summary>
    public static ScreenCapture CaptureAllScreens(Window owner, out double maxScaling)
    {
        var screens = owner.Screens?.All;
        if (screens == null || screens.Count == 0)
            throw new InvalidOperationException("No screens available.");

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        maxScaling = owner.RenderScaling;
        foreach (var screen in screens)
        {
            var b = screen.Bounds;
            if (b.X < minX) minX = b.X;
            if (b.Y < minY) minY = b.Y;
            if (b.Right > maxX) maxX = b.Right;
            if (b.Bottom > maxY) maxY = b.Bottom;
            if (screen.Scaling > maxScaling) maxScaling = screen.Scaling;
        }

        return Capture(minX, minY, maxX - minX, maxY - minY);
    }

    public static unsafe ScreenCapture Capture(int x, int y, int width, int height)
    {
        // Без обнуления: массив всё равно целиком перезапишет GetDIBits
        var pixels = GC.AllocateUninitializedArray<uint>(width * height);

        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        try
        {
            var oldObject = SelectObject(memDc, bitmap);
            var copied = BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SRCCOPY) != 0;
            // GetDIBits требует, чтобы битмап не был выбран в DC
            SelectObject(memDc, oldObject);
            if (!copied)
                throw new InvalidOperationException("BitBlt failed.");

            var info = new BITMAPINFO();
            info.Header.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.Header.biWidth = width;
            info.Header.biHeight = -height; // отрицательная высота = строки сверху вниз
            info.Header.biPlanes = 1;
            info.Header.biBitCount = 32;
            info.Header.biCompression = BI_RGB;

            fixed (uint* dst = pixels)
            {
                if (GetDIBits(screenDc, bitmap, 0, (uint)height, dst, &info, DIB_RGB_COLORS) == 0)
                    throw new InvalidOperationException("GetDIBits failed.");
            }
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        MakeOpaque(pixels);
        return new ScreenCapture(x, y, width, height, pixels);
    }

    /// <summary>Цвет пикселя в формате 0xAARRGGBB (координаты зажимаются в границы снимка).</summary>
    public uint GetArgb(int x, int y)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        return _pixels[y * Width + x];
    }

    public Color GetPixel(int x, int y) => Color.FromUInt32(GetArgb(x, y));

    public unsafe WriteableBitmap ToBitmap()
    {
        var bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888);
        using var frame = bitmap.Lock();
        var rowBytes = Width * 4;
        fixed (uint* src = _pixels)
        {
            var srcBytes = (byte*)src;
            var dstBytes = (byte*)frame.Address;
            if (frame.RowBytes == rowBytes)
            {
                var total = (long)rowBytes * Height;
                Buffer.MemoryCopy(srcBytes, dstBytes, total, total);
            }
            else
            {
                for (var row = 0; row < Height; row++)
                    Buffer.MemoryCopy(srcBytes + (long)row * rowBytes, dstBytes + (long)row * frame.RowBytes, frame.RowBytes, rowBytes);
            }
        }

        return bitmap;
    }

    // GDI оставляет альфа-канал нулевым - выставляем 0xFF векторно (миллионы пикселей)
    private static void MakeOpaque(uint[] pixels)
    {
        var span = pixels.AsSpan();
        var vectors = MemoryMarshal.Cast<uint, System.Numerics.Vector<uint>>(span);
        var mask = new System.Numerics.Vector<uint>(0xFF000000u);
        for (var i = 0; i < vectors.Length; i++)
            vectors[i] |= mask;

        for (var i = vectors.Length * System.Numerics.Vector<uint>.Count; i < span.Length; i++)
            span[i] |= 0xFF000000u;
    }

    #region Win32

    private const uint SRCCOPY = 0x00CC0020;
    private const uint BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        // Место под маски/палитру, чтобы GetDIBits гарантированно не писал за пределы структуры
        public uint Color0;
        public uint Color1;
        public uint Color2;
        public uint Color3;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern int BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    private static extern unsafe int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, void* lpvBits, BITMAPINFO* lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern int DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern int DeleteDC(IntPtr hdc);

    #endregion
}
