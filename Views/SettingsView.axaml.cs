// SettingsView.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorPickerApp.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace ColorPickerApp.Views
{
    public partial class SettingsView : UserControl
    {
        SettingsViewModel? ViewModel;
        public SettingsView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            ViewModel = DataContext as SettingsViewModel;
            HotkeyTextBox.KeyDown += OnHotkeyKeyDown;
        }

        private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            var modifiers = e.KeyModifiers;
            var key = e.Key;

            // Игнорируем одиночные клавиши-модификаторы
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            string hotkey = "";
            if (modifiers.HasFlag(KeyModifiers.Control))
                hotkey += "Ctrl+";
            if (modifiers.HasFlag(KeyModifiers.Shift))
                hotkey += "Shift+";
            if (modifiers.HasFlag(KeyModifiers.Alt))
                hotkey += "Alt+";

            hotkey += key.ToString();
            ViewModel.PickerHotkey = hotkey;
            e.Handled = true;
        }
    }
}