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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Globalization;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using System.Threading.Tasks;

namespace ColorPickerApp.Views;

public partial class MainWindow : Window
{
    private const int SwatchSlotsCount = 20;
    private const int MaxHarmonyColors = 3;
    private static readonly string SwatchTip = Loc.T("swatch_tip");

    private static readonly IBrush SwatchBorderBrush = new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

    private static readonly (ColorHarmonyMode Mode, string Title)[] HarmonyModes =
    {
        (ColorHarmonyMode.Single, Loc.T("harmony_single")),
        (ColorHarmonyMode.Complementary, Loc.T("harmony_complementary")),
        (ColorHarmonyMode.Triad, Loc.T("harmony_triad")),
        (ColorHarmonyMode.Tetrad, Loc.T("harmony_tetrad")),
        (ColorHarmonyMode.Analogous, Loc.T("harmony_analogous")),
        (ColorHarmonyMode.AnalogousAccent, Loc.T("harmony_accent")),
    };

    private readonly Border[] _swatchFills = new Border[SwatchSlotsCount];
    private readonly Button[] _harmonyButtons = new Button[MaxHarmonyColors];
    private readonly Border[] _harmonyFills = new Border[MaxHarmonyColors];

    private MainWindowViewModel? _subscribedVm;
    private DispatcherTimer? _copiedLabelTimer;
    private bool _picking;
    private bool _historyDirty = true;

    // Шкала оттенков
    private readonly Border[] _scaleSwatches = new Border[ShadeScale.Steps.Length];
    private readonly TextBlock[] _scaleHexTexts = new TextBlock[ShadeScale.Steps.Length];
    private readonly TextBlock[] _scaleOklchTexts = new TextBlock[ShadeScale.Steps.Length];
    private Color[] _scaleColors = Array.Empty<Color>();
    private readonly List<Grid> _scaleRowGrids = new();
    private bool _scaleBuilt;
    private bool _scaleDirty = true;
    private bool _suppressScaleRebuild;
    private bool _helpBuilt;

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

        // История, шкала и справка строятся только когда их вкладку видно
        App.Settings.RecentColorsChanged += OnRecentColorsChanged;
        RightTabs.SelectionChanged += OnTabsSelectionChanged;

        InitScaleOptions();

        // Раскладка вкладок по колонкам - до первого показа, чтобы CenterScreen учёл ширину окна
        ApplyLayout();

        Topmost = App.Settings.AlwaysOnTop;
        App.Settings.PropertyChanged += OnAppSettingsChanged;
    }

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettingsStore.AlwaysOnTop):
                Topmost = App.Settings.AlwaysOnTop;
                break;
            case nameof(AppSettingsStore.HexUppercase):
                // Тексты HEX в шкале и подсказках истории - в новом регистре
                RefreshScaleTexts();
                _historyDirty = true;
                if (IsShown(HistoryTab))
                    RebuildHistory();
                break;
            case nameof(AppSettingsStore.Layout):
                ApplyLayout();
                break;
        }
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
            App.SaveSessionState(); // программа может так и закончиться в трее (выключение ПК)
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
                OnColorChangedForScale();
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

    #region Шкала оттенков

    private void InitScaleOptions()
    {
        ScaleNameBox.Text = App.Settings.ScaleName;
        ScaleExportBox.SelectedIndex = Math.Clamp(App.Settings.ScaleExportIndex, 0, 3);
        ScaleValueBox.SelectedIndex = Math.Clamp(App.Settings.ScaleValueIndex, 0, 2);

        ScaleExportBox.SelectionChanged += (_, _) =>
        {
            App.Settings.ScaleExportIndex = ScaleExportBox.SelectedIndex;
            App.Settings.Save();
        };
        ScaleValueBox.SelectionChanged += (_, _) =>
        {
            App.Settings.ScaleValueIndex = ScaleValueBox.SelectedIndex;
            App.Settings.Save();
        };
        ScaleNameBox.LostFocus += (_, _) => SaveScaleName();

        // Сняли «Закрепить» - сразу показываем шкалу от текущего цвета
        ScalePinnedBox.IsCheckedChanged += (_, _) =>
        {
            if (ScalePinnedBox.IsChecked != true)
            {
                _scaleDirty = true;
                if (IsShown(ScaleTab))
                    UpdateScale();
            }
        };
    }

    private void SaveScaleName()
    {
        var name = ScaleNameBox.Text ?? "";
        if (App.Settings.ScaleName == name)
            return;
        App.Settings.ScaleName = name;
        App.Settings.Save();
    }

    private void OnColorChangedForScale()
    {
        // Клик по ступени и закреплённая шкала не перестраивают её
        if (_suppressScaleRebuild || ScalePinnedBox.IsChecked == true)
            return;

        _scaleDirty = true;
        if (IsShown(ScaleTab))
            UpdateScale();
    }

    private void ShowScale()
    {
        EnsureScaleRows();
        if (_scaleDirty)
            UpdateScale();
    }

    private void EnsureScaleRows()
    {
        if (_scaleBuilt)
            return;
        _scaleBuilt = true;

        for (var i = 0; i < ShadeScale.Steps.Length; i++)
        {
            var index = i;
            var swatch = new Border
            {
                Height = 20,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
            };
            var step = new TextBlock
            {
                Text = ShadeScale.Steps[i].ToString(CultureInfo.InvariantCulture),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var hex = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var oklch = new TextBlock
            {
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new ImmutableSolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8)),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(ScaleRowColumns(_scaleCompact)), ColumnSpacing = 8 };
            _scaleRowGrids.Add(grid);
            Grid.SetColumn(step, 1);
            Grid.SetColumn(hex, 2);
            Grid.SetColumn(oklch, 3);
            grid.Children.Add(swatch);
            grid.Children.Add(step);
            grid.Children.Add(hex);
            grid.Children.Add(oklch);

            var row = new Button { Content = grid };
            row.Classes.Add("scaleRow");
            ToolTip.SetTip(row, Loc.T("scale_row_tip"));
            row.Click += (_, _) => OnScaleStepClick(index);

            _scaleSwatches[i] = swatch;
            _scaleHexTexts[i] = hex;
            _scaleOklchTexts[i] = oklch;
            ScaleRowsPanel.Children.Add(row);
        }
    }

    private void UpdateScale()
    {
        if (Vm is null)
            return;

        EnsureScaleRows();
        _scaleDirty = false;

        var (colors, baseIndex) = ShadeScale.Generate(Vm.OpaqueColor);
        _scaleColors = colors;
        for (var i = 0; i < colors.Length; i++)
        {
            _scaleSwatches[i].Background = new ImmutableSolidColorBrush(colors[i]);
            _scaleSwatches[i].BorderBrush = i == baseIndex ? Brushes.White : Brushes.Transparent;
            _scaleHexTexts[i].Text = ColorFormat.Hex(colors[i]);
            _scaleOklchTexts[i].Text = ColorFormat.Oklch(colors[i]);
        }
    }

    private void RefreshScaleTexts()
    {
        if (!_scaleBuilt)
            return;
        for (var i = 0; i < _scaleColors.Length; i++)
            _scaleHexTexts[i].Text = ColorFormat.Hex(_scaleColors[i]);
    }

    private void OnScaleStepClick(int index)
    {
        if (Vm is null || index >= _scaleColors.Length)
            return;

        var color = _scaleColors[index];
        _suppressScaleRebuild = true;
        try
        {
            Vm.SetColor(Color.FromArgb(Vm.SelectedColor.A, color.R, color.G, color.B));
        }
        finally
        {
            _suppressScaleRebuild = false;
        }
    }

    private async void OnCopyScaleClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is null)
            return;
        if (_scaleColors.Length == 0)
            UpdateScale();
        if (_scaleColors.Length == 0)
            return;

        SaveScaleName();
        var text = ShadeScale.Export(_scaleColors, ScaleNameBox.Text,
            Math.Max(0, ScaleExportBox.SelectedIndex), Math.Max(0, ScaleValueBox.SelectedIndex));
        await Clipboard.SetTextAsync(text);
        ShowCopiedLabel(1000);
    }

    #endregion

    #region История

    private void OnRecentColorsChanged()
    {
        _historyDirty = true;
        if (IsShown(HistoryTab))
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
                ? ColorFormat.Hex(color)
                : Loc.F("history_alpha", ColorFormat.Hex(color), color.A / 255.0));

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

    #region Раскладка вкладок по колонкам

    private readonly List<TabControl> _extraTabControls = new();
    private readonly Dictionary<TabItem, TabControl> _tabHosts = new();
    private int _leftColumnCount;
    private bool _applyingLayout;
    private bool _layoutApplied;
    private Dictionary<string, TabItem>? _movableTabs;
    private bool _oklchCompact;
    private bool _contrastCompact;
    private bool _scaleCompact;

    private Dictionary<string, TabItem> MovableTabs => _movableTabs ??= new()
    {
        [WindowLayout.Oklch] = OklchTab,
        [WindowLayout.Scale] = ScaleTab,
        [WindowLayout.Contrast] = ContrastTab,
        [WindowLayout.History] = HistoryTab,
        [WindowLayout.Help] = HelpTab,
    };

    /// <summary>
    /// Вкладка сейчас на экране: стоит в какой-то колонке и выбрана в ней.
    /// Смотрим на SelectedItem колонки, а не на TabItem.IsSelected: у только что перенесённой вкладки
    /// IsSelected обновится лишь после первого прохода layout.
    /// </summary>
    private bool IsShown(TabItem tab) =>
        tab == ValuesTab
            ? RightTabs.SelectedItem == ValuesTab
            : _tabHosts.TryGetValue(tab, out var host) && host.SelectedItem == tab;

    private void OnTabsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateVisibleTabs();
        if (!_applyingLayout)
            SaveOpenTabs();
    }

    private string KeyOf(TabItem tab) =>
        tab == ValuesTab ? WindowLayout.Values : MovableTabs.First(p => p.Value == tab).Key;

    /// <summary>Запоминает открытые вкладки всех колонок - при следующем запуске откроются они же.</summary>
    private void SaveOpenTabs()
    {
        var open = _extraTabControls.Append(RightTabs)
            .Select(t => t.SelectedItem)
            .OfType<TabItem>()
            .Select(KeyOf)
            .ToList();
        if (open.SequenceEqual(App.Settings.OpenTabs))
            return;
        App.Settings.OpenTabs = open;
        App.Settings.Save();
    }

#if DEBUG
    internal void SelectTabForSnapshot(string key)
    {
        if (MovableTabs.TryGetValue(key, out var tab) && tab.Parent is TabControl tabs)
            tabs.SelectedItem = tab;
    }

    internal void OpenSettingsPaneForSnapshot() => ToggleButtonHide.IsChecked = true;

    internal void SelectSettingsTabForSnapshot(int index)
    {
        if (SettingsHost.Content is Control settings
            && settings.GetLogicalDescendants().OfType<TabControl>().FirstOrDefault() is { } tabs)
            tabs.SelectedIndex = index;
    }
#endif

    private void UpdateVisibleTabs()
    {
        if (IsShown(HistoryTab) && _historyDirty)
            RebuildHistory();
        if (IsShown(ScaleTab))
            ShowScale();
        if (IsShown(HelpTab) && !_helpBuilt)
        {
            // Справка из Assets/help.<язык>.md - строится один раз при первом открытии
            _helpBuilt = true;
            MarkdownView.Render(HelpPanel, MarkdownView.LoadHelp());
        }
    }

    /// <summary>
    /// Раскладывает вкладки по колонкам из настроек. Сами вкладки (со всем содержимым и состоянием)
    /// переносятся между TabControl'ами, а не создаются заново.
    /// </summary>
    private void ApplyLayout()
    {
        var layout = App.Settings.Layout;
        _applyingLayout = true;

        // Какие вкладки были открыты - останутся открытыми и на новом месте.
        // При запуске - те, что были открыты в прошлый раз.
        var wasSelected = new HashSet<TabItem>();
        if (!_layoutApplied)
        {
            foreach (var key in App.Settings.OpenTabs)
            {
                if (key == WindowLayout.Values)
                    wasSelected.Add(ValuesTab);
                else if (MovableTabs.TryGetValue(key, out var saved))
                    wasSelected.Add(saved);
            }
        }
        else
        {
            foreach (var tabs in _extraTabControls.Append(RightTabs))
            {
                if (tabs.SelectedItem is TabItem selected)
                    wasSelected.Add(selected);
            }
        }

        // Сначала освобождаем содержимое: элемент не может стоять в двух местах визуального дерева
        RightTabs.SelectedItem = ValuesTab;
        foreach (var tabs in _extraTabControls)
        {
            tabs.SelectionChanged -= OnTabsSelectionChanged;
            tabs.Items.Clear();
        }
        foreach (var tab in MovableTabs.Values)
        {
            RightTabs.Items.Remove(tab);
            // Не IsSelected = false: локальное значение TabControl считает «главнее» своего выбора
            // и тогда не подсвечивает заголовок выбранной вкладки в новой колонке
            tab.ClearValue(TabItem.IsSelectedProperty);
        }
        LeftColumnsHost.Children.Clear();
        ExtraColumnsHost.Children.Clear();
        _extraTabControls.Clear();
        _tabHosts.Clear();

        // Основная колонка: «Значения» всегда первая
        foreach (var key in layout.MainColumn)
            Place(MovableTabs[key], RightTabs, compact: false);

        // Дополнительные колонки слева и справа от основной
        for (var i = 0; i < layout.Columns.Count; i++)
        {
            if (i == layout.MainIndex)
                continue;

            var left = i < layout.MainIndex;
            var tabs = new TabControl { Padding = new Thickness(0, 6, 0, 0) };
            var column = new Border
            {
                Width = WindowLayout.ColumnWidth,
                Margin = left
                    ? new Thickness(0, 0, WindowLayout.ColumnSpacing, 0)
                    : new Thickness(WindowLayout.ColumnSpacing, 0, 0, 0),
                Padding = new Thickness(12, 4, 12, 12),
                Child = tabs,
            };
            column.Classes.Add("panel");
            (left ? LeftColumnsHost : ExtraColumnsHost).Children.Add(column);
            _extraTabControls.Add(tabs);

            foreach (var key in layout.Columns[i])
                Place(MovableTabs[key], tabs, compact: true);
        }

        foreach (var tabs in _extraTabControls.Append(RightTabs))
        {
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().FirstOrDefault(wasSelected.Contains)
                                ?? tabs.Items.OfType<TabItem>().FirstOrDefault();
        }
        foreach (var tabs in _extraTabControls)
            tabs.SelectionChanged += OnTabsSelectionChanged;

        ResizeForLayout(layout.WindowWidth, layout.LeftColumnCount - _leftColumnCount);
        _leftColumnCount = layout.LeftColumnCount;
        _applyingLayout = false;
        _layoutApplied = true;
        UpdateVisibleTabs();
        SaveOpenTabs();
    }

    private void Place(TabItem tab, TabControl tabs, bool compact)
    {
        tabs.Items.Add(tab);
        _tabHosts[tab] = tabs;

        if (tab == OklchTab)
            SetOklchCompact(compact);
        else if (tab == ContrastTab)
            SetContrastCompact(compact);
        else if (tab == ScaleTab)
            SetScaleCompact(compact);
    }

    /// <param name="leftColumnsAdded">На сколько колонок слева стало больше (меньше - отрицательное).</param>
    private void ResizeForLayout(double width, int leftColumnsAdded)
    {
        if (Math.Abs(Width - width) < 0.5 && leftColumnsAdded == 0)
            return;

        Width = width;

        // Окно ещё не показано - его отцентрирует CenterScreen
        if (!IsVisible)
            return;
        var screen = Screens?.ScreenFromVisual(this) ?? Screens?.Primary;
        if (screen is null)
            return;

        // Колонки слева добавляются «наружу»: левый край окна сдвигается, основная колонка остаётся на месте
        var x = Position.X - (int)Math.Round(leftColumnsAdded * (WindowLayout.ColumnWidth + WindowLayout.ColumnSpacing) * screen.Scaling);

        // И окно не должно уходить за края экрана
        var area = screen.WorkingArea;
        var pixelWidth = (int)Math.Ceiling(width * screen.Scaling);
        if (x + pixelWidth > area.Right)
            x = area.Right - pixelWidth;
        if (x < area.X)
            x = area.X;
        if (x != Position.X)
            Position = new PixelPoint(x, Position.Y);
    }

    // В узкой колонке круг тона над ползунками, в широкой - слева от них
    private void SetOklchCompact(bool compact)
    {
        if (_oklchCompact == compact)
            return;
        _oklchCompact = compact;

        Grid.SetColumnSpan(OklchRing, compact ? 2 : 1);
        OklchRing.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        Grid.SetRow(OklchSliders, compact ? 1 : 0);
        Grid.SetColumn(OklchSliders, compact ? 0 : 1);
        Grid.SetColumnSpan(OklchSliders, compact ? 2 : 1);
        OklchGrid.RowSpacing = compact ? 10 : 0;
    }

    // В узкой колонке: отношение под превью, уровни WCAG строками, кнопки подбора под списком
    private void SetContrastCompact(bool compact)
    {
        if (_contrastCompact == compact)
            return;
        _contrastCompact = compact;

        Grid.SetRow(ContrastRatioBlock, compact ? 1 : 0);
        Grid.SetColumn(ContrastRatioBlock, compact ? 0 : 1);
        Grid.SetColumnSpan(ContrastRatioBlock, compact ? 2 : 1);
        ContrastRatioBlock.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        ContrastHeadGrid.RowSpacing = compact ? 4 : 0;

        Control[] labels = { LevelNormalLabel, LevelLargeLabel, LevelUiLabel };
        Control[] badges = { LevelNormalBadges, LevelLargeBadges, LevelUiBadge };
        ContrastLevelsGrid.ColumnDefinitions = new ColumnDefinitions(compact ? "Auto,*" : "Auto,Auto,Auto");
        ContrastLevelsGrid.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto,Auto" : "Auto,Auto");
        ContrastLevelsGrid.ColumnSpacing = compact ? 14 : 22;
        ContrastLevelsGrid.RowSpacing = compact ? 4 : 2;
        ContrastLevelsGrid.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        for (var i = 0; i < labels.Length; i++)
        {
            Grid.SetRow(labels[i], compact ? i : 0);
            Grid.SetColumn(labels[i], compact ? 0 : i);
            Grid.SetRow(badges[i], compact ? i : 1);
            Grid.SetColumn(badges[i], compact ? 1 : i);
            labels[i].VerticalAlignment = VerticalAlignment.Center;
        }

        ContrastSuggestGrid.ColumnDefinitions = new ColumnDefinitions(compact ? "*,*" : "*,Auto,Auto");
        ContrastSuggestGrid.RowSpacing = compact ? 6 : 0;
        Grid.SetColumnSpan(ContrastTargetBox, compact ? 2 : 1);
        Grid.SetRow(SuggestTextButton, compact ? 1 : 0);
        Grid.SetColumn(SuggestTextButton, compact ? 0 : 1);
        Grid.SetRow(SuggestBgButton, compact ? 1 : 0);
        Grid.SetColumn(SuggestBgButton, compact ? 1 : 2);
        SuggestTextButton.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        SuggestBgButton.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }

    private static string ScaleRowColumns(bool compact) => compact ? "44,36,74,*" : "72,40,74,*";

    private void SetScaleCompact(bool compact)
    {
        if (_scaleCompact == compact)
            return;
        _scaleCompact = compact;
        foreach (var grid in _scaleRowGrids)
            grid.ColumnDefinitions = new ColumnDefinitions(ScaleRowColumns(compact));
    }

    #endregion

    #region SplitView

    private Point _mousePos;
    private bool _splitViewIsHidden = true;
    private bool _splitViewAnimationRunning;
    private Animation? _showSplitViewAnimation;
    private Animation? _hideSplitViewAnimation;

    // Панель настроек создаётся только когда пользователь к ней тянется:
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
