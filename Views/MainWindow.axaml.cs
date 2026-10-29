using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using ColorPickerApp.Controls;
using ColorPickerApp.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ColorPickerApp.Views;

public partial class MainWindow : Window
{
    private const int SwatchSlotsCount = 20;
    private const int MaxHarmonyColors = 3;
    private const string SwatchTip = "ЛКМ: применить сохранённый\nПКМ: пересохранить текущий\nСКМ: сброс";

    private static readonly IBrush SwatchBorderBrush = new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

    private static readonly (ColorHarmonyMode Mode, string Title)[] HarmonyModes =
    {
        (ColorHarmonyMode.Single, "Моно"),
        (ColorHarmonyMode.Complementary, "Контраст"),
        (ColorHarmonyMode.Triad, "Триада"),
        (ColorHarmonyMode.Tetrad, "Тетрада"),
        (ColorHarmonyMode.Analogous, "Аналогия"),
        (ColorHarmonyMode.AnalogousAccent, "Акцент аналогия"),
    };

    private readonly Border[] _swatchFills = new Border[SwatchSlotsCount];
    private readonly Button[] _harmonyButtons = new Button[MaxHarmonyColors];
    private readonly Border[] _harmonyFills = new Border[MaxHarmonyColors];

    private MainWindowViewModel? _subscribedVm;
    private DispatcherTimer? _copiedLabelTimer;
    private bool _picking;
    private bool _historyDirty = true;

    public MainWindow()
    {
        InitializeComponent();

        // Всё строим до первого кадра (раньше - в Opened, что давало лишний проход layout
        // и повторные подписки на хоткеи при каждом показе окна из трея)
        latticeToggleButton.IsChecked = App.Settings.IsCopyWithFunction;
        EnsureSwatchesStorage();
        BuildSwatches();
        BuildHarmonySwatches();

        DataContextChanged += OnDataContextChanged;
        Wheel.PropertyChanged += Wheel_PropertyChanged;
        ToggleButtonHide.IsCheckedChanged += (_, _) => EnsureSettingsView();

        // Вкладка истории строится только когда её открывают
        App.Settings.RecentColorsChanged += OnRecentColorsChanged;
        RightTabs.SelectionChanged += (_, _) =>
        {
            if (HistoryTab.IsSelected && _historyDirty)
                RebuildHistory();
        };
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    #region Окно / трей

    public void Open()
    {
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Show();
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Завершение приложения / выход из Windows - не мешаем закрытию
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
            return;

        if (!App.Settings.PermanentCloseWindow)
        {
            e.Cancel = true;
            Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Окно закрыто по-настоящему - завершаем приложение (ShutdownMode = OnExplicitShutdown)
        App.Quit();
    }

    #endregion

    private void Wheel_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ColorWheelSvControl.HarmonyAngleProperty || e.Property == ColorWheelSvControl.HarmonyModeProperty)
            UpdateHarmonySwatches();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = Vm;
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        RefreshCopyText();
        UpdateHarmonySwatches();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // SelectedColor поднимается при любом изменении цвета (последним в RaiseAll),
        // поэтому реагируем один раз, а не на каждое из 7 свойств, как раньше
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.SelectedColor):
                RefreshCopyText();
                UpdateHarmonySwatches();
                break;
            case nameof(MainWindowViewModel.CopyFormat):
            case nameof(MainWindowViewModel.CopyWithAlpha):
                RefreshCopyText();
                break;
        }
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs? e)
    {
        if (Vm is null || Clipboard is null)
            return;

        RefreshCopyText();
        await Clipboard.SetTextAsync(CopyValueBox.Text);
        App.Settings.AddRecentColor(Vm.SelectedColor);
        ShowCopiedLabel(1000);
    }

    private void ShowCopiedLabel(int delayMs)
    {
        lableContent.IsVisible = true;
        if (_copiedLabelTimer is null)
        {
            _copiedLabelTimer = new DispatcherTimer();
            _copiedLabelTimer.Tick += (_, _) =>
            {
                lableContent.IsVisible = false;
                _copiedLabelTimer?.Stop();
            };
        }

        _copiedLabelTimer.Stop();
        _copiedLabelTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        _copiedLabelTimer.Start();
    }

    private void ButtonNextColorMode_OnClick(object? sender, RoutedEventArgs e) => StepHarmonyMode(+1);

    private void ButtonBackColorMode_OnClick(object? sender, RoutedEventArgs e) => StepHarmonyMode(-1);

    private void StepHarmonyMode(int delta)
    {
        var index = Array.FindIndex(HarmonyModes, m => m.Mode == Wheel.HarmonyMode);
        var next = Math.Clamp(index + delta, 0, HarmonyModes.Length - 1);
        if (next == index)
            return;

        Wheel.HarmonyMode = HarmonyModes[next].Mode; // свотчи обновит Wheel_PropertyChanged
        ColorModeLabel.Content = HarmonyModes[next].Title;
    }

    #region Пипетка

    // Хоткей / трей: восстанавливать окно - по настройке
    public async void ShowEyedropper() => await PickWithEyedropperAsync(App.Settings.RestoreWindowAfterPick);

    // Кнопка в окне: окно всегда возвращаем
    private async void OnEyedropperClick(object? sender, RoutedEventArgs e) => await PickWithEyedropperAsync(restoreWindow: true);

    private async Task PickWithEyedropperAsync(bool restoreWindow)
    {
        if (Vm is null || _picking)
            return;

        _picking = true;
        try
        {
            // Ждём анимацию сворачивания, только если окно действительно на экране.
            // Из трея (окно скрыто) пипетка открывается без задержки в 200 мс.
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                WindowState = WindowState.Minimized;
                await Task.Delay(200);
            }

            var picked = await EyedropperOverlayWindow.PickAsync(this);
            if (picked is not Color color)
                return;

            Vm.SetColor(color);
            App.Settings.AddRecentColor(color);

            if (restoreWindow)
                Open();

            if (App.Settings.AutoCopyOnPick)
                OnCopyClick(null, null);
        }
        catch (Exception ex)
        {
            // Например, BitBlt недоступен на защищённом рабочем столе - не роняем приложение
            System.Diagnostics.Trace.WriteLine(ex);
        }
        finally
        {
            _picking = false;
        }
    }

    #endregion

    #region Формат копирования

    private void OnCopyFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || sender is not ComboBox box)
            return;
        var item = box.SelectedItem as ComboBoxItem;
        Vm.CopyFormat = (item?.Content?.ToString() ?? "rgb").ToLowerInvariant();
        latticeToggleButton.Content = (Vm.CopyFormat == "hex" ? '#' : 'ƒ');
        latticeToggleButton.IsEnabled = Vm.CopyFormat != "rgb-565";
        switchWithAlpha.IsEnabled = Vm.CopyFormat != "rgb-565";
        RefreshCopyText();
    }

    private void latticeToggleButton_Checked(object? sender, RoutedEventArgs e)
    {
        var isChecked = latticeToggleButton.IsChecked == true;
        if (App.Settings.IsCopyWithFunction != isChecked)
        {
            App.Settings.IsCopyWithFunction = isChecked;
            App.Settings.Save();
        }
        RefreshCopyText();
    }

    private void RefreshCopyText()
    {
        if (Vm is null)
            return;

        var text = Vm.GetColorAsText();
        if (latticeToggleButton.IsChecked != true)
        {
            text = text.Replace("#", "");

            int start = text.IndexOf('(') + 1;
            int end = text.LastIndexOf(')');
            if (start > 0 && end > start)
                text = text.Substring(start, end - start);
        }

        CopyValueBox.Text = text;
    }

    #endregion

    #region Свотчи

    private static (Button Button, Border Fill) CreateSwatchButton(double width, Thickness margin, double height = 26)
    {
        var fill = new Border();
        var button = new Button
        {
            Width = width,
            Height = height,
            Margin = margin,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            BorderBrush = SwatchBorderBrush,
            Background = Brushes.Transparent,
            Content = new Border
            {
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Child = new Grid
                {
                    Children =
                    {
                        new CheckerboardControl { CellSize = 4 },
                        fill,
                    },
                },
            },
        };
        button.Classes.Add("swatcherButton");
        return (button, fill);
    }

    private void BuildHarmonySwatches()
    {
        for (var i = 0; i < MaxHarmonyColors; i++)
        {
            var (button, fill) = CreateSwatchButton(80, new Thickness(0, 0, 8, 8));
            button.IsVisible = false;
            button.Click += (_, _) =>
            {
                if (Vm is not null && fill.Background is ISolidColorBrush brush)
                    Vm.SetColor(brush.Color);
            };

            _harmonyButtons[i] = button;
            _harmonyFills[i] = fill;
            HarmonyPanel.Children.Add(button);
        }
    }

    // Раньше на каждое изменение цвета (каждое движение мыши по кругу) кнопки создавались заново,
    // теперь меняется только кисть заливки у уже существующих
    private void UpdateHarmonySwatches()
    {
        if (Vm is null)
            return;

        var alpha = Vm.SelectedColor.A;
        var i = 0;
        foreach (var color in Wheel.GetHarmonyColors())
        {
            if (i >= MaxHarmonyColors)
                break;
            _harmonyFills[i].Background = new ImmutableSolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            _harmonyButtons[i].IsVisible = true;
            i++;
        }

        for (; i < MaxHarmonyColors; i++)
            _harmonyButtons[i].IsVisible = false;
    }

    private void EnsureSwatchesStorage()
    {
        var swatches = App.Settings.SavedSwatches;
        var changed = false;
        while (swatches.Count < SwatchSlotsCount)
        {
            swatches.Add(string.Empty);
            changed = true;
        }
        if (swatches.Count > SwatchSlotsCount)
        {
            swatches.RemoveRange(SwatchSlotsCount, swatches.Count - SwatchSlotsCount);
            changed = true;
        }
        if (changed)
            App.Settings.Save();
    }

    private void BuildSwatches()
    {
        SwatchesPanel.Children.Clear();
        for (var i = 0; i < SwatchSlotsCount; i++)
        {
            var idx = i;
            var (button, fill) = CreateSwatchButton(26, new Thickness(0, 0, 4, 4));
            ToolTip.SetTip(button, SwatchTip);
            button.Click += (_, _) => ApplySwatch(idx);
            button.PointerPressed += (_, e) => OnSwatchPointerPressed(idx, e);

            _swatchFills[i] = fill;
            SwatchesPanel.Children.Add(button);
            UpdateSwatchVisual(i);
        }
    }

    private void SaveCurrentToSwatch(int index)
    {
        if (Vm is null || index < 0 || index >= App.Settings.SavedSwatches.Count)
            return;
        var color = Vm.SelectedColor;
        App.Settings.SavedSwatches[index] = $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
        App.Settings.Save();
        UpdateSwatchVisual(index);
    }

    private void ApplySwatch(int index)
    {
        if (Vm is null || index < 0 || index >= App.Settings.SavedSwatches.Count)
            return;
        var hex = App.Settings.SavedSwatches[index];
        if (!string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color))
            Vm.SetColor(color);
    }

    private void UpdateSwatchVisual(int index)
    {
        if (index < 0 || index >= SwatchSlotsCount)
            return;

        var hex = App.Settings.SavedSwatches[index];
        _swatchFills[index].Background = Color.TryParse(hex, out var color)
            ? new ImmutableSolidColorBrush(color)
            : Brushes.Transparent;
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
        UpdateSwatchVisual(index);
    }

    #endregion

    #region История

    private void OnRecentColorsChanged()
    {
        _historyDirty = true;
        if (HistoryTab.IsSelected)
            RebuildHistory();
    }

    private void RebuildHistory()
    {
        _historyDirty = false;
        HistoryPanel.Children.Clear();

        var colors = App.Settings.RecentColors;
        for (var i = 0; i < colors.Count; i++)
        {
            if (!Color.TryParse(colors[i], out var color))
                continue;

            var index = i;
            var (button, fill) = CreateSwatchButton(34, new Thickness(0, 0, 6, 6), 34);
            fill.Background = new ImmutableSolidColorBrush(color);
            ToolTip.SetTip(button, color.A == 255
                ? ColorMath.ToHex(color)
                : $"{ColorMath.ToHex(color)}, альфа {color.A / 255.0:0.00}");

            button.Click += (_, _) => Vm?.SetColor(color);
            button.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                {
                    App.Settings.RemoveRecentColor(index);
                    e.Handled = true;
                }
            };
            HistoryPanel.Children.Add(button);
        }

        HistoryEmptyText.IsVisible = HistoryPanel.Children.Count == 0;
    }

    private void OnClearHistoryClick(object? sender, RoutedEventArgs e) => App.Settings.ClearRecentColors();

    #endregion

    #region OKLCH

    private void OnOklchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitOklch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Vm is not null)
        {
            OklchBox.SetCurrentValue(TextBox.TextProperty, Vm.Oklch);
            e.Handled = true;
        }
    }

    private void OnOklchLostFocus(object? sender, RoutedEventArgs e) => CommitOklch();

    private void CommitOklch()
    {
        if (Vm is null || OklchBox.Text == Vm.Oklch)
            return;

        // Не разобралось - возвращаем текущее значение (SetCurrentValue не ломает привязку)
        if (!Vm.TrySetOklch(OklchBox.Text))
            OklchBox.SetCurrentValue(TextBox.TextProperty, Vm.Oklch);
    }

    #endregion

    #region SplitView

    private Point _mousePos;
    private bool _splitViewIsHidden = true;
    private bool _splitViewAnimationRunning;
    private Animation? _showSplitViewAnimation;
    private Animation? _hideSplitViewAnimation;

    // Панель настроек (с тяжёлым ColorPicker) создаётся только когда пользователь к ней тянется:
    // при наведении на край окна или по клику на кнопку
    private void EnsureSettingsView()
    {
        if (SettingsHost.Content is null)
            SettingsHost.Content = new SettingsView();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        _mousePos = e.GetPosition(this);
        if (!SplitViewControl.IsPaneOpen)
            CheckMouse();
        else
            SplitViewControl.Margin = new Thickness(0, 0, 0, 0);

        base.OnPointerMoved(e);
    }

    private void CheckMouse()
    {
        if (_mousePos.X >= 0 && _mousePos.X < 20)
            ShowSplitView();
        else
            HideSplitView();
    }

    private static Animation CreateMarginAnimation(Thickness from, Thickness to) => new()
    {
        Duration = TimeSpan.FromMilliseconds(200),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0.0),
                Setters = { new Setter(Control.MarginProperty, from) }
            },
            new KeyFrame
            {
                Cue = new Cue(1.0),
                Setters = { new Setter(Control.MarginProperty, to) }
            }
        }
    };

    private async void ShowSplitView()
    {
        if (!_splitViewIsHidden || _splitViewAnimationRunning) return;

        EnsureSettingsView();
        _showSplitViewAnimation ??= CreateMarginAnimation(new Thickness(-20, 0, 0, 0), new Thickness(0));

        _splitViewAnimationRunning = true;
        await _showSplitViewAnimation.RunAsync(SplitViewControl);
        _splitViewAnimationRunning = false;
        _splitViewIsHidden = false;
        CheckMouse();
    }

    public async void HideSplitView()
    {
        if (_splitViewIsHidden || _splitViewAnimationRunning) return;

        _hideSplitViewAnimation ??= CreateMarginAnimation(new Thickness(0), new Thickness(-20, 0, 0, 0));

        _splitViewAnimationRunning = true;
        await _hideSplitViewAnimation.RunAsync(SplitViewControl);
        _splitViewAnimationRunning = false;
        _splitViewIsHidden = true;
        CheckMouse();
    }

    #endregion
}
