using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ColorPickerApp.Controls;
using ColorPickerApp.Services;
using ColorPickerApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ColorPickerApp.Views;

public partial class MainWindow : Window
{
    private const int SwatchSlotsCount = 20;
    private MainWindowViewModel? _subscribedVm;
    private DispatcherTimer _timerLisibleCopyText;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        if (StartupHelper.IsAutoStartMode)
        {
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
            Hide();
        }

        Opened += (_, _) =>
        {
            latticeToggleButton.IsChecked = App.Settings.IsCopyWithFunction;
            EnsureSwatchesStorage();
            BuildSwatches();

            var hotkeyService = App.ServiceProvider.GetRequiredService<GlobalHotkeyService>();
            hotkeyService.HotkeyOpenAppPressed += () => Dispatcher.UIThread.Post(Open);
            hotkeyService.HotkeyOpenPipetePressed += () => Dispatcher.UIThread.Post(ShowEyedropper);
            
        };
        Wheel.PropertyChanged += Wheel_PropertyChanged;
    }

    private bool _closeInTray = false;

    public void ClosePermanent()
    {
        _closeInTray = true;
        Close();
    }
    public void Open()
    {
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Show();
        Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        var hotkeyService = App.ServiceProvider.GetRequiredService<GlobalHotkeyService>();
        hotkeyService.Stop();
        
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = !App.Settings.PermanentCloseWindow && !_closeInTray;
        if (!_closeInTray)
        {
            Hide();
        }
    }

    private void Wheel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name is nameof(ColorWheelSvControl.HarmonyAngle))
        {
            UpdateAdditionalColors();
        }
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = Vm;
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        RefreshCopyText();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.CopyFormat) or nameof(MainWindowViewModel.CopyWithAlpha)
            or nameof(MainWindowViewModel.R) or nameof(MainWindowViewModel.G) or nameof(MainWindowViewModel.B)
            or nameof(MainWindowViewModel.Hue) or nameof(MainWindowViewModel.Saturation) or nameof(MainWindowViewModel.Value)
            or nameof(MainWindowViewModel.Alpha))
        {
            RefreshCopyText();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.SelectedColor) or nameof(ColorWheelSvControl.HarmonyAngle))
        {
            UpdateAdditionalColors();
        }
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || Clipboard is null)
            return;

        var tb = this.FindControl<TextBox>("CopyValueBox");
        if (tb is null)
            return;

        await Clipboard.SetTextAsync(tb.Text);
        RefreshCopyText();
        ChangeTextWithTimer(1000);
    }

    private async void ButtonNextColorMode_OnClick(object? sender, RoutedEventArgs e)
    {
        switch (Wheel.HarmonyMode)
        {
            case ColorHarmonyMode.Single:
                Wheel.HarmonyMode = ColorHarmonyMode.Complementary;
                ColorModeLabel.Content = "Контраст";
                break;
            case ColorHarmonyMode.Complementary:
                Wheel.HarmonyMode = ColorHarmonyMode.Triad;
                ColorModeLabel.Content = "Триада";
                break;
            case ColorHarmonyMode.Triad:
                Wheel.HarmonyMode = ColorHarmonyMode.Tetrad;
                ColorModeLabel.Content = "Тетрада";
                break;
            case ColorHarmonyMode.Tetrad:
                Wheel.HarmonyMode = ColorHarmonyMode.Analogous;
                ColorModeLabel.Content = "Аналогия";
                break;
            case ColorHarmonyMode.Analogous:
                Wheel.HarmonyMode = ColorHarmonyMode.AnalogousAccent;
                ColorModeLabel.Content = "Акцент аналогия";
                break;
            case ColorHarmonyMode.AnalogousAccent:
                break;
        }

        UpdateAdditionalColors();
    }

    private async void ButtonBackColorMode_OnClick(object? sender, RoutedEventArgs e)
    {
        switch (Wheel.HarmonyMode)
        {
            case ColorHarmonyMode.Single:
                break;
            case ColorHarmonyMode.Complementary:
                Wheel.HarmonyMode = ColorHarmonyMode.Single;
                ColorModeLabel.Content = "Моно";
                break;
            case ColorHarmonyMode.Triad:
                Wheel.HarmonyMode = ColorHarmonyMode.Complementary;
                ColorModeLabel.Content = "Контраст";
                break;
            case ColorHarmonyMode.Tetrad:
                Wheel.HarmonyMode = ColorHarmonyMode.Triad;
                ColorModeLabel.Content = "Триада";
                break;
            case ColorHarmonyMode.Analogous:
                Wheel.HarmonyMode = ColorHarmonyMode.Tetrad;
                ColorModeLabel.Content = "Тетрада";
                break;
            case ColorHarmonyMode.AnalogousAccent:
                Wheel.HarmonyMode = ColorHarmonyMode.Analogous;
                ColorModeLabel.Content = "Аналогия";
                break;
        }

        UpdateAdditionalColors();
    }

    private void ChangeTextWithTimer(int delayMs)
    {
        lableContent.IsVisible = true;
        if (_timerLisibleCopyText != null)
            _timerLisibleCopyText.Stop();
        _timerLisibleCopyText = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _timerLisibleCopyText.Tick += (s, e) =>
        {
            lableContent.IsVisible = false;
            _timerLisibleCopyText.Stop();
        };
        _timerLisibleCopyText.Start();
    }


    private EyedropperOverlayWindow? eyedropperOverlayWindow;
    public async Task<Color?> ShowAndPickColor(Window owner)
    {
        var (capture, bounds, renderScaling) = EyedropperOverlayWindow.CaptureAllScreens(owner);
        eyedropperOverlayWindow = new EyedropperOverlayWindow(capture, bounds, renderScaling);

        var tcs = new TaskCompletionSource<Color?>();

        void OnClosed(object? s, EventArgs e)
        {
            eyedropperOverlayWindow.Closed -= OnClosed;
            tcs.TrySetResult(eyedropperOverlayWindow.Result);
            capture.Dispose();
        }

        eyedropperOverlayWindow.Closed += OnClosed;

        eyedropperOverlayWindow.Show();

        var result = await tcs.Task;
        await Task.Delay(50);

        return result;
    }

    public async void ShowEyedropper()
    {
        if (Vm is null)
            return;

        if (eyedropperOverlayWindow != null && eyedropperOverlayWindow.IsVisible)
            return;

        this.WindowState = WindowState.Minimized;
        await Task.Delay(200);
        var overlayResult = await ShowAndPickColor(this);


        if (overlayResult is Color color)
        {
            Vm.SetColor(color);
            RefreshCopyText();


            if (App.Settings.RestoreWindowAfterPick)
            {
                this.WindowState = WindowState.Normal;
                this.Focus();
            }

            if (App.Settings.AutoCopyOnPick)
            {
                OnCopyClick(null, null);
            }
        }
    }

    private async void OnEyedropperClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        this.WindowState = WindowState.Minimized;
        await Task.Delay(200);
        var overlayResult = await ShowAndPickColor(this);


        if (overlayResult is Color color)
        {
            Vm.SetColor(color);
            RefreshCopyText();

            this.WindowState = WindowState.Normal;
            this.Focus();

            if (App.Settings.AutoCopyOnPick)
            {
                OnCopyClick(null, null);
            }
        }
    }

    private void OnCopyFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || sender is not ComboBox box)
            return;
        var item = box.SelectedItem as ComboBoxItem;
        Vm.CopyFormat = (item?.Content?.ToString() ?? "rgb").ToLowerInvariant();
        latticeToggleButton.Content = (Vm.CopyFormat == "hex" ? '#' : 'ƒ');
        RefreshCopyText();
    }
    private void latticeToggleButton_Checked(object? sender, RoutedEventArgs e)
    {
        App.Settings.IsCopyWithFunction = latticeToggleButton.IsChecked.Value;
        App.Settings.Save();
        RefreshCopyText();
    }

    private void RefreshCopyText()
    {
        if (Vm is null)
            return;

        var text = Vm.GetColorAsText();
        var tb = this.FindControl<TextBox>("CopyValueBox");
        if (!latticeToggleButton.IsChecked.Value)
        {
            if (text.Contains('#'))
                text = text.Replace("#", "");

            int start = text.IndexOf('(') + 1;
            int end = text.LastIndexOf(')');
            if(start != -1 && end != -1)
                text = text.Substring(start, end - start);
        }

        if (tb is not null)
            tb.Text = text;
    }

    private void EnsureSwatchesStorage()
    {
        while (App.Settings.SavedSwatches.Count < SwatchSlotsCount)
            App.Settings.SavedSwatches.Add(string.Empty);
        if (App.Settings.SavedSwatches.Count > SwatchSlotsCount)
            App.Settings.SavedSwatches = App.Settings.SavedSwatches.Take(SwatchSlotsCount).ToList();
        App.Settings.Save();
    }

    private void UpdateAdditionalColors()
    {
        if (Vm is null)
            return;

        Vm.AdditionalColors.Clear();
        var idx = 0;
        foreach (var color in Wheel.GetHarmonyColors())
        {
            var resultColor = Color.FromArgb(Vm.SelectedColor.A, color.R, color.G, color.B);
            var swatchButton = new Button
            {
                Width = 80,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.Parse("#60ffffff")),
                Background = Brushes.Transparent,
                Content = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    ClipToBounds = true,
                    Child = new Grid
                    {
                        Children =
                        {
                            new Controls.CheckerboardControl { CellSize = 4 },
                            new Border { Name = $"SwatchFill{idx}", Background = new SolidColorBrush(resultColor) },
                        },
                    },
                },
            };
            idx++;
            swatchButton.Click += OnSwatchClick;
            swatchButton.Classes.Add("swatcherButton");
            Vm.AdditionalColors.Add(swatchButton);
        }


    }

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        if (sender is not Button btn || btn.Content is not Border b || b.Child is not Grid g || g.Children.Count < 2 || g.Children[1] is not Border fill || fill.Background is not SolidColorBrush brush)
            return;

        Vm.SetColor(brush.Color);
    }

    private void BuildSwatches()
    {
        var panel = this.FindControl<WrapPanel>("SwatchesPanel");
        if (panel is null)
            return;

        panel.Children.Clear();
        for (var i = 0; i < SwatchSlotsCount; i++)
        {
            var idx = i;
            var swatchButton = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.Parse("#60ffffff")),
                Background = Brushes.Transparent,
                Content = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    ClipToBounds = true,
                    Child = new Grid
                    {
                        Children =
                        {
                            new Controls.CheckerboardControl { CellSize = 4 },
                            new Border { Name = $"SwatchFill{idx}" },
                        },
                    },
                },
            };

            swatchButton.Classes.Add("swatcherButton");
            swatchButton.Click += (_, _) => ApplySwatch(idx);
            swatchButton.PointerPressed += (_, e) => OnSwatchPointerPressed(idx, e);
            panel.Children.Add(swatchButton);
            UpdateSwatchVisual(panel, idx);
        }
    }

    private void SaveCurrentToSwatch(int index)
    {
        if (Vm is null || index < 0 || index >= App.Settings.SavedSwatches.Count)
            return;
        var color = Vm.SelectedColor;
        var hex = $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
        App.Settings.SavedSwatches[index] = hex;
        App.Settings.Save();

        var panel = this.FindControl<WrapPanel>("SwatchesPanel");
        if (panel is not null)
            UpdateSwatchVisual(panel, index);
    }

    private void ApplySwatch(int index)
    {
        if (Vm is null || index < 0 || index >= App.Settings.SavedSwatches.Count)
            return;
        var hex = App.Settings.SavedSwatches[index];
        if (string.IsNullOrWhiteSpace(hex))
            return;
        if (Color.TryParse(hex, out var color))
            Vm.SetColor(color);
    }

    private static void UpdateSwatchVisual(WrapPanel panel, int index)
    {
        if (index >= panel.Children.Count || panel.Children[index] is not Button btn || btn.Content is not Border b
            || b.Child is not Grid g || g.Children.Count < 2 || g.Children[1] is not Border fill)
            return;

        var hex = App.Settings.SavedSwatches[index];
        fill.Background = Color.TryParse(hex, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush(Color.Parse("#00ffffff"));
        ToolTip.SetTip(btn, "ЛКМ: применить сохранённый\nПКМ: пересохранить текущий\nСКМ: сброс");
    }

    private void OnSwatchPointerPressed(int index, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            SaveCurrentToSwatch(index);
            e.Handled = true;
            return;
        }
        if (props.IsMiddleButtonPressed)
        {
            ResetSwatch(index);
            e.Handled = true;
        }
    }

    private void ResetSwatch(int index)
    {
        if (Vm is null || index < 0 || index >= App.Settings.SavedSwatches.Count)
            return;

        App.Settings.SavedSwatches[index] = string.Empty;
        App.Settings.Save();

        var panel = this.FindControl<WrapPanel>("SwatchesPanel");
        if (panel is not null)
            UpdateSwatchVisual(panel, index);
    }

    #region SplitView
    Point mousePos;

    protected override void OnPointerMoved(PointerEventArgs e)
    {

        mousePos = e.GetPosition(this);
        if (!SplitViewControl.IsPaneOpen)
        {
            CheckMouse();
        }
        else
            SplitViewControl.Margin = new Thickness(0, 0, 0, 0);

        base.OnPointerMoved(e);
    }

    private void CheckMouse()
    {
        if (mousePos.X >= 0 && mousePos.X < 20)
            ShowSplitView();
        else
            HideSplitView();
    }

    private bool splitViewIsHide = true;
    private bool splitViewAnimationRunning = false;
    private async void ShowSplitView()
    {
        if (!splitViewIsHide || splitViewAnimationRunning) return;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters =
                    {
                        new Setter(Control.MarginProperty, new Thickness(-20, 0, 0, 0))
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters =
                    {
                        new Setter(Control.MarginProperty, new Thickness(0))
                    }
                }
            }
        };

        splitViewAnimationRunning = true;
        await animation.RunAsync(SplitViewControl);
        splitViewAnimationRunning = false;
        splitViewIsHide = false;
        CheckMouse();
    }

    public async void HideSplitView()
    {
        if (splitViewIsHide || splitViewAnimationRunning) return;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters =
                    {
                        new Setter(Control.MarginProperty, new Thickness(0))
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters =
                    {
                        new Setter(Control.MarginProperty, new Thickness(-20, 0, 0, 0))
                    }
                }
            }
        };

        splitViewAnimationRunning = true;
        await animation.RunAsync(SplitViewControl);
        splitViewAnimationRunning = false;
        splitViewIsHide = true;
        CheckMouse();
    }


    #endregion
}