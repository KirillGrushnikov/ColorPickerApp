using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace ColorPickerApp.Views;

/// <summary>
/// Редактор раскладки в настройках: мини-схема окна, вкладки-«чипы» перетаскиваются мышью
/// между колонками, в новую колонку и в «Скрыто». Изменения применяются к окну сразу.
/// Перетаскивание сделано на событиях указателя (без системного drag-and-drop) - работает только внутри схемы.
/// </summary>
public sealed class LayoutEditor : UserControl
{
    private const double SchemeHeight = 150;
    private const double MainBoxWidth = 260;
    private const double ColumnBoxWidth = 104;
    private const double DragThreshold = 4;

    private static readonly IBrush BoxBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush BoxBorder = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush FixedBlockBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush ChipBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush ChipBorder = new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush LockedChipBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush DimText = new ImmutableSolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly IBrush HighlightBorder = new ImmutableSolidColorBrush(Color.FromRgb(0x7B, 0xD8, 0x8F));
    private static readonly IBrush HighlightBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x26, 0x7B, 0xD8, 0x8F));
    private static readonly IBrush WarnText = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x65));

    /// <summary>Место, куда можно бросить вкладку.</summary>
    private sealed class DropZone
    {
        public required Border Box { get; init; }
        public required int Column { get; init; }
        public List<(Border Chip, string Tab)> Chips { get; } = new();
        public required IBrush NormalBackground { get; init; }
    }

    private readonly Grid _schemeHost;
    private readonly StackPanel _scheme;
    private readonly Canvas _overlay;
    private readonly TextBlock _widthText;
    private readonly TextBlock _warnText;
    private readonly Button _classicButton;
    private readonly Button _designerButton;
    private readonly List<DropZone> _zones = new();

    private WindowLayout _layout = App.Settings.Layout;

    // Перетаскивание
    private Border? _pressedChip;
    private string? _pressedTab;
    private Point _pressPoint;
    private Border? _ghost;
    private DropZone? _hoverZone;

    public LayoutEditor()
    {
        _classicButton = CreatePresetButton("layout_classic", "layout_classic_tip", () => WindowLayout.Classic);
        _designerButton = CreatePresetButton("layout_designer", "layout_designer_tip", () => WindowLayout.Designer);

        _widthText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = DimText, Margin = new Thickness(8, 0, 0, 0) };
        _warnText = new TextBlock { Foreground = WarnText, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };

        var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        presets.Children.Add(new TextBlock { Text = Loc.T("layout_presets"), VerticalAlignment = VerticalAlignment.Center });
        presets.Children.Add(_classicButton);
        presets.Children.Add(_designerButton);
        presets.Children.Add(_widthText);

        _scheme = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 6) };
        _overlay = new Canvas { IsHitTestVisible = false };
        _schemeHost = new Grid
        {
            Children =
            {
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = _scheme,
                },
                _overlay,
            },
        };

        var hint = new TextBlock
        {
            Text = Loc.T("layout_hint"),
            Foreground = DimText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        Content = new StackPanel
        {
            Spacing = 8,
            Children = { presets, _schemeHost, _warnText, hint },
        };

        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        App.Settings.PropertyChanged += OnSettingsChanged;
        _layout = App.Settings.Layout;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        App.Settings.PropertyChanged -= OnSettingsChanged;
    }

    // Раскладку могли поменять и снаружи (сброс настроек)
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppSettingsStore.Layout))
            return;
        _layout = App.Settings.Layout;
        Rebuild();
    }

    private Button CreatePresetButton(string titleKey, string tipKey, Func<WindowLayout> preset)
    {
        var button = new Button { Content = Loc.T(titleKey), Padding = new Thickness(10, 4) };
        ToolTip.SetTip(button, Loc.T(tipKey));
        button.Click += (_, _) => Apply(preset());
        return button;
    }

    private void Apply(WindowLayout layout)
    {
        _layout = layout;
        App.Settings.SetLayout(layout); // окно перестроится, а схема - через OnSettingsChanged
        Rebuild();
    }

    #region Схема

    private void Rebuild()
    {
        _scheme.Children.Clear();
        _zones.Clear();

        // Слева направо, как в окне: [+ новая слева] [колонки слева] [основная] [колонки справа] [+ новая справа] [скрыто]
        _scheme.Children.Add(CreateNewColumnBox(WindowLayout.NewLeftColumn));
        var number = 1;
        for (var i = 0; i < _layout.Columns.Count; i++)
        {
            _scheme.Children.Add(i == _layout.MainIndex ? CreateMainBox() : CreateColumnBox(i, number));
            if (i != _layout.MainIndex)
                number++;
        }
        _scheme.Children.Add(CreateNewColumnBox(WindowLayout.NewRightColumn));
        _scheme.Children.Add(CreateHiddenBox());

        UpdateInfo();
    }

    private void UpdateInfo()
    {
        var width = _layout.WindowWidth;
        _widthText.Text = Loc.F("layout_width", Math.Round(width));

        var available = GetScreenWidth();
        _warnText.IsVisible = available > 0 && width > available;
        if (_warnText.IsVisible)
            _warnText.Text = Loc.F("layout_too_wide", Math.Round(width), Math.Round(available));

        SetAccent(_classicButton, _layout.SameAs(WindowLayout.Classic));
        SetAccent(_designerButton, _layout.SameAs(WindowLayout.Designer));
    }

    private static void SetAccent(Button button, bool on)
    {
        if (on)
            button.Classes.Add("accent");
        else
            button.Classes.Remove("accent");
    }

    /// <summary>Ширина рабочей области экрана в единицах окна (0 - неизвестно).</summary>
    private double GetScreenWidth()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var screens = topLevel?.Screens;
        var screen = topLevel is null ? screens?.Primary : screens?.ScreenFromVisual(topLevel) ?? screens?.Primary;
        if (screen is null || screen.Scaling <= 0)
            return 0;
        return screen.WorkingArea.Width / screen.Scaling;
    }

    // Основная колонка: пипетка | вкладки (первая - «Значения», её не двигают), снизу - полоса цвета
    private Control CreateMainBox()
    {
        var zone = CreateZone(_layout.MainIndex, out var chipsPanel);
        chipsPanel.Children.Add(CreateLockedChip());
        AddChips(zone, chipsPanel, _layout.MainColumn);

        var eyedropper = CreateFixedBlock(Loc.T("layout_eyedropper"));
        var strip = CreateFixedBlock(Loc.T("layout_strip"));
        strip.Height = 22;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("78,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 6,
            RowSpacing = 6,
        };
        Grid.SetColumn(zone.Box, 1);
        Grid.SetRow(strip, 1);
        Grid.SetColumnSpan(strip, 2);
        grid.Children.Add(eyedropper);
        grid.Children.Add(zone.Box);
        grid.Children.Add(strip);

        return CreateBox(Loc.T("layout_main"), grid, MainBoxWidth);
    }

    private Control CreateColumnBox(int column, int number)
    {
        var zone = CreateZone(column, out var chipsPanel);
        AddChips(zone, chipsPanel, _layout.Columns[column]);
        return CreateBox(Loc.F("layout_column", number), zone.Box, ColumnBoxWidth);
    }

    /// <param name="column"><see cref="WindowLayout.NewLeftColumn"/> или <see cref="WindowLayout.NewRightColumn"/>.</param>
    private Control CreateNewColumnBox(int column)
    {
        var zone = CreateZone(column, out var chipsPanel);
        chipsPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("layout_new_column"),
            Foreground = DimText,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 30, 0, 0),
        });
        chipsPanel.HorizontalAlignment = HorizontalAlignment.Center;
        return CreateBox(" ", zone.Box, ColumnBoxWidth);
    }

    private Control CreateHiddenBox()
    {
        var zone = CreateZone(WindowLayout.HiddenColumn, out var chipsPanel);
        AddChips(zone, chipsPanel, _layout.Hidden);
        if (_layout.Hidden.Count == 0)
        {
            chipsPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("layout_hidden_empty"),
                Foreground = DimText,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
            });
            chipsPanel.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var box = CreateBox(Loc.T("layout_hidden"), zone.Box, ColumnBoxWidth + 10);
        box.Margin = new Thickness(16, 0, 0, 0);
        return box;
    }

    /// <summary>Подпись сверху и рамка-колонка.</summary>
    private static Control CreateBox(string title, Control content, double width)
    {
        var caption = new TextBlock { Text = title, FontSize = 11.5, Foreground = DimText, Margin = new Thickness(2, 0, 0, 3) };
        DockPanel.SetDock(caption, Dock.Top);
        return new DockPanel
        {
            Width = width,
            Height = SchemeHeight + 18,
            Children = { caption, content },
        };
    }

    private DropZone CreateZone(int column, out WrapPanel chipsPanel)
    {
        chipsPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        var background = column == _layout.MainIndex ? FixedBlockBackground : BoxBackground;
        var box = new Border
        {
            Background = background,
            BorderBrush = BoxBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(5),
            Child = chipsPanel,
        };
        var zone = new DropZone { Box = box, Column = column, NormalBackground = background };
        _zones.Add(zone);
        return zone;
    }

    private static Border CreateFixedBlock(string text) => new()
    {
        Background = FixedBlockBackground,
        BorderBrush = BoxBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = DimText,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private static Border CreateChipVisual(string text, IBrush background) => new()
    {
        Background = background,
        BorderBrush = ChipBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(6, 2),
        Margin = new Thickness(0, 0, 4, 4),
        Child = new TextBlock { Text = text, FontSize = 11.5 },
    };

    private static Control CreateLockedChip()
    {
        var chip = CreateChipVisual(WindowLayout.Title(WindowLayout.Values), LockedChipBackground);
        chip.Opacity = 0.75;
        ToolTip.SetTip(chip, Loc.T("layout_locked_tip"));
        return chip;
    }

    private void AddChips(DropZone zone, Panel panel, IEnumerable<string> tabs)
    {
        foreach (var tab in tabs)
        {
            var chip = CreateChipVisual(WindowLayout.Title(tab), ChipBackground);
            chip.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(chip, Loc.T("layout_chip_tip"));

            var key = tab;
            chip.PointerPressed += (_, e) => OnChipPressed(chip, key, e);
            chip.PointerMoved += (_, e) => OnChipMoved(chip, e);
            chip.PointerReleased += (_, e) => OnChipReleased(chip, e);
            chip.PointerCaptureLost += (_, _) =>
            {
                if (_pressedChip == chip)
                    EndDrag();
            };

            zone.Chips.Add((chip, key));
            panel.Children.Add(chip);
        }
    }

    #endregion

    #region Перетаскивание

    private void OnChipPressed(Border chip, string tab, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(chip).Properties.IsLeftButtonPressed)
            return;

        _pressedChip = chip;
        _pressedTab = tab;
        _pressPoint = e.GetPosition(_schemeHost);
        e.Pointer.Capture(chip);
        e.Handled = true;
    }

    private void OnChipMoved(Border chip, PointerEventArgs e)
    {
        if (_pressedChip != chip || _pressedTab is null)
            return;

        var point = e.GetPosition(_schemeHost);
        if (_ghost is null)
        {
            var delta = point - _pressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                return;
            StartDrag(chip);
        }

        if (_ghost is not null)
        {
            Canvas.SetLeft(_ghost, point.X - chip.Bounds.Width / 2);
            Canvas.SetTop(_ghost, point.Y - chip.Bounds.Height / 2);
        }
        SetHoverZone(FindZone(point));
    }

    private void OnChipReleased(Border chip, PointerReleasedEventArgs e)
    {
        if (_pressedChip != chip || _pressedTab is null)
            return;

        var tab = _pressedTab;
        var dragged = _ghost is not null;
        var point = e.GetPosition(_schemeHost);
        var zone = dragged ? FindZone(point) : null;
        var index = zone is null ? 0 : FindInsertIndex(zone, point);

        EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (zone is null)
            return;

        var layout = _layout.Clone();
        layout.Move(tab, zone.Column, index);
        if (!layout.SameAs(_layout))
            Apply(layout);
    }

    private void StartDrag(Border chip)
    {
        chip.Opacity = 0.35;
        _ghost = CreateChipVisual(WindowLayout.Title(_pressedTab!), ChipBackground);
        _ghost.Opacity = 0.9;
        _ghost.BorderBrush = HighlightBorder;
        _overlay.Children.Add(_ghost);
    }

    private void EndDrag()
    {
        if (_pressedChip is not null)
            _pressedChip.Opacity = 1;
        if (_ghost is not null)
            _overlay.Children.Remove(_ghost);

        _ghost = null;
        _pressedChip = null;
        _pressedTab = null;
        SetHoverZone(null);
    }

    private void SetHoverZone(DropZone? zone)
    {
        if (_hoverZone == zone)
            return;

        if (_hoverZone is not null)
        {
            _hoverZone.Box.BorderBrush = BoxBorder;
            _hoverZone.Box.Background = _hoverZone.NormalBackground;
        }

        _hoverZone = zone;
        if (zone is not null)
        {
            zone.Box.BorderBrush = HighlightBorder;
            zone.Box.Background = HighlightBackground;
        }
    }

    private Rect? BoundsInHost(Control control)
    {
        var topLeft = control.TranslatePoint(default, _schemeHost);
        return topLeft is { } p ? new Rect(p, control.Bounds.Size) : null;
    }

    private DropZone? FindZone(Point point)
    {
        // За пределами видимой схемы (прокрученные колонки) не бросаем
        if (!new Rect(_schemeHost.Bounds.Size).Contains(point))
            return null;

        foreach (var zone in _zones)
        {
            if (BoundsInHost(zone.Box) is { } rect && rect.Contains(point))
                return zone;
        }
        return null;
    }

    /// <summary>Позиция вставки по порядку чтения: перед первым чипом, который правее/ниже указателя.</summary>
    private int FindInsertIndex(DropZone zone, Point point)
    {
        for (var i = 0; i < zone.Chips.Count; i++)
        {
            if (BoundsInHost(zone.Chips[i].Chip) is not { } rect)
                continue;
            var before = point.Y < rect.Top || (point.Y <= rect.Bottom && point.X < rect.Center.X);
            if (before)
                return i;
        }
        return zone.Chips.Count;
    }

    #endregion
}
