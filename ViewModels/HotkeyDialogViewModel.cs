using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Input;
using ColorPickerApp.ViewModels;

namespace ColorPickerApp.ViewModels
{
    public class HotkeyDialogViewModel : ViewModelBase
    {
        private string _currentHotkey = "";
        public string CurrentHotkey
        {
            get => _currentHotkey;
            set => this.RaiseAndSetIfChanged(ref _currentHotkey, value);
        }

        public ReactiveCommand<Unit, string?> ConfirmCommand { get; }
        public ReactiveCommand<Unit, string?> CancelCommand { get; }

        public HotkeyDialogViewModel()
        {
            // При подтверждении возвращаем текущую комбинацию
            ConfirmCommand = ReactiveCommand.Create(() => CurrentHotkey);
            // При отмене возвращаем null
            CancelCommand = ReactiveCommand.Create(() => (string?)null);
        }

        // Метод для обновления комбинации из события KeyDown
        public void UpdateHotkeyFromKeyDown(object? sender, KeyEventArgs e)
        {
            var hotkey = GetStringHotkeyKeyDown(sender, e);
            if (hotkey != null)
            {
                CurrentHotkey = hotkey;
            }
        }

        private string? GetStringHotkeyKeyDown(object? sender, KeyEventArgs e)
        {
            var modifiers = e.KeyModifiers;
            var key = e.Key;

            // Игнорируем одиночные клавиши-модификаторы
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return null;
            }

            string hotkey = "";
            if (modifiers.HasFlag(KeyModifiers.Control))
                hotkey += "Ctrl+";
            if (modifiers.HasFlag(KeyModifiers.Shift))
                hotkey += "Shift+";
            if (modifiers.HasFlag(KeyModifiers.Alt))
                hotkey += "Alt+";

            hotkey += key.ToString();
            e.Handled = true;
            return hotkey;
        }
    }
}