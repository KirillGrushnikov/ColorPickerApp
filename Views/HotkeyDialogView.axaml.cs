using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorPickerApp.ViewModels;
using System;

namespace ColorPickerApp.Views
{
    public partial class HotkeyDialogView : Window
    {
        private HotkeyDialogViewModel? _viewModel;

        public HotkeyDialogView()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;
            // Tunnel: кнопки мыши ловим раньше, чем их получат кнопки диалога
            AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        }

        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            var button = props.IsXButton1Pressed ? "Mouse4"
                : props.IsXButton2Pressed ? "Mouse5"
                : props.IsMiddleButtonPressed ? "MouseMiddle"
                : null;
            if (button is null)
                return;

            (DataContext as HotkeyDialogViewModel)?.UpdateHotkeyFromMouse(e.KeyModifiers, button);
            e.Handled = true;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Пока назначаем клавишу, глобальные хоткеи не должны срабатывать
            if (App.Hotkeys is { } hotkeys)
                hotkeys.IsSuspended = true;

            _viewModel = DataContext as HotkeyDialogViewModel;
            if (_viewModel != null)
                _viewModel.CloseRequested += OnCloseRequested;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_viewModel != null)
                _viewModel.CloseRequested -= OnCloseRequested;

            if (App.Hotkeys is { } hotkeys)
                hotkeys.IsSuspended = false;
        }

        private void OnCloseRequested(string? hotkey) => Close(hotkey);

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            (DataContext as HotkeyDialogViewModel)?.UpdateHotkeyFromKeyDown(sender, e);
        }
    }
}
