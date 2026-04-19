using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reflection;

namespace ColorPickerApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool _internalUpdate;
    private double _hue = 0;
    private double _saturation = 1.0;
    private double _value = 1.0;
    private double _alpha = 1.0;
    private int _r;
    private int _g;
    private int _b;
    private string _hex = "#000000";
    private string _copyFormat = "rgb";
    private int _copyFormatIndex = 0;
    private bool _copyWithAlpha = true;

    public ObservableCollection<Button> AdditionalColors { get; } = new();

    public MainWindowViewModel()
    {
        CopyCommand = ReactiveCommand.Create(() => { });
        SetFromHsv(_hue, _saturation, _value, _alpha);

        CopyFormatIndex = App.Settings.CopyFormatIndex;
        CopyWithAlpha = (bool)App.Settings.IsCopyWithAlpha;
    }

    public ReactiveCommand<Unit, Unit> CopyCommand { get; }

    public double Hue
    {
        get => _hue;
        set
        {
            this.RaiseAndSetIfChanged(ref _hue, WrapHue(value));
            UpdateFromHsv();
        }
    }

    public double Saturation
    {
        get => _saturation;
        set
        {
            this.RaiseAndSetIfChanged(ref _saturation, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public double Alpha
    {
        get => _alpha;
        set
        {
            this.RaiseAndSetIfChanged(ref _alpha, Math.Clamp(value, 0, 1));
            UpdateFromHsv();
        }
    }

    public int R
    {
        get => _r;
        set
        {
            this.RaiseAndSetIfChanged(ref _r, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public int G
    {
        get => _g;
        set
        {
            this.RaiseAndSetIfChanged(ref _g, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public int B
    {
        get => _b;
        set
        {
            this.RaiseAndSetIfChanged(ref _b, Math.Clamp(value, 0, 255));
            UpdateFromRgb();
        }
    }

    public double NormalR
    {
        get => _r / 255.0;
        set
        {
            R = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public double NormalG
    {
        get => _g / 255.0;
        set
        {
            G = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public double NormalB
    {
        get => _b / 255.0;
        set
        {
            B = (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
            this.RaisePropertyChanged();
        }
    }

    public string Hex
    {
        get => _hex;
        set
        {
            this.RaiseAndSetIfChanged(ref _hex, value);
            TryUpdateFromHex(value);
        }
    }

    public int CopyFormatIndex
    {
        get => _copyFormatIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _copyFormatIndex, value);
            App.Settings.CopyFormatIndex = value;
            App.Settings.Save();
        }
    }

    public string CopyFormat
    {
        get => _copyFormat;
        set => this.RaiseAndSetIfChanged(ref _copyFormat, value);
    }

    public bool CopyWithAlpha
    {
        get => _copyWithAlpha;
        set
        {
            this.RaiseAndSetIfChanged(ref _copyWithAlpha, value);
            App.Settings.IsCopyWithAlpha = value;
            App.Settings.Save();
        }
    }

    public Color SelectedColor => Color.FromArgb((byte)Math.Round(_alpha * 255), (byte)_r, (byte)_g, (byte)_b);
    public Color OpaqueColor => Color.FromArgb(255, (byte)_r, (byte)_g, (byte)_b);
    public SolidColorBrush SelectedBrush => new(SelectedColor);

    public string GetColorAsText()
    {
        return CopyFormat switch
        {
            "hsv" => CopyWithAlpha
                ? $"{Hue:F0}, {(Saturation * 100):F0}%, {(Value * 100):F0}%, {Alpha:F2}"
                : $"{Hue:F0}, {(Saturation * 100):F0}%, {(Value * 100):F0}%",
            "normal" => CopyWithAlpha
                ? $"{NormalR:F2}, {NormalG:F2}, {NormalB:F2}, {Alpha:F2}"
                : $"{NormalR:F2}, {NormalG:F2}, {NormalB:F2}",
            "hex" => CopyWithAlpha
                ? $"#{(int)Math.Round(Alpha * 255):x2}{R:x2}{G:x2}{B:x2}"
                : $"#{R:x2}{G:x2}{B:x2}",
            _ => CopyWithAlpha
                ? $"rgba({R}, {G}, {B}, {Alpha:F2})"
                : $"rgb({R}, {G}, {B})",
        };
    }

    public void SetColor(Color color)
    {
        var hsv = ColorMath.ToHsv(color);
        SetFromHsv(hsv.H, hsv.S, hsv.V, color.A / 255.0);
    }

    private void UpdateFromHsv()
    {
        if (_internalUpdate)
            return;
        SetFromHsv(_hue, _saturation, _value, _alpha);
    }

    private void UpdateFromRgb()
    {
        if (_internalUpdate)
            return;

        _internalUpdate = true;
        var color = Color.FromArgb((byte)Math.Round(_alpha * 255), (byte)_r, (byte)_g, (byte)_b);
        var hsv = ColorMath.ToHsv(color);
        _hue = hsv.H;
        _saturation = hsv.S;
        _value = hsv.V;
        _hex = $"#{_r:x2}{_g:x2}{_b:x2}";
        RaiseAll();
        _internalUpdate = false;
    }

    private void SetFromHsv(double h, double s, double v, double a)
    {
        _internalUpdate = true;
        _hue = WrapHue(h);
        _saturation = Math.Clamp(s, 0, 1);
        _value = Math.Clamp(v, 0, 1);
        _alpha = Math.Clamp(a, 0, 1);

        var color = ColorMath.FromHsv(_hue, _saturation, _value, _alpha);
        _r = color.R;
        _g = color.G;
        _b = color.B;
        _hex = $"#{_r:x2}{_g:x2}{_b:x2}";
        RaiseAll();
        _internalUpdate = false;
    }

    private void TryUpdateFromHex(string? text)
    {
        if (_internalUpdate || string.IsNullOrWhiteSpace(text))
            return;

        var value = text.Trim().TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return;

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        _r = r;
        _g = g;
        _b = b;
        UpdateFromRgb();
    }

    private static double WrapHue(double hue)
    {
        var h = hue % 360;
        if (h < 0) h += 360;
        return h;
    }

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(Hue));
        this.RaisePropertyChanged(nameof(Saturation));
        this.RaisePropertyChanged(nameof(Value));
        this.RaisePropertyChanged(nameof(Alpha));
        this.RaisePropertyChanged(nameof(R));
        this.RaisePropertyChanged(nameof(G));
        this.RaisePropertyChanged(nameof(B));
        this.RaisePropertyChanged(nameof(NormalR));
        this.RaisePropertyChanged(nameof(NormalG));
        this.RaisePropertyChanged(nameof(NormalB));
        this.RaisePropertyChanged(nameof(Hex));
        this.RaisePropertyChanged(nameof(SelectedColor));
        this.RaisePropertyChanged(nameof(OpaqueColor));
        this.RaisePropertyChanged(nameof(SelectedBrush));
    }
}
