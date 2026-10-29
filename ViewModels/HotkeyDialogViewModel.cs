using Avalonia.Input;
using ColorPickerApp.Services;
using System;

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

        private string? _errorText;
        public string? ErrorText
        {
            get => _errorText;
            set => this.RaiseAndSetIfChanged(ref _errorText, value);
        }

        /// <summary>Второй хоткей приложения - с ним новая комбинация не должна совпадать.</summary>
        public string? OtherHotkey { get; init; }

        /// <summary>Для сообщения об ошибке: "уже назначена на {OtherHotkeyName}".</summary>
        public string OtherHotkeyName { get; init; } = "другое действие";

        /// <summary>Окно подписывается и закрывается с переданным результатом (null - отмена).</summary>
        public event Action<string?>? CloseRequested;

        // Привязаны к кнопкам в HotkeyDialogView.axaml
        public void Confirm()
        {
            var hotkey = CurrentHotkey;

            ErrorText = GlobalHotkeyService.Validate(hotkey);
            if (ErrorText != null)
                return;

            if (GlobalHotkeyService.AreSame(hotkey, OtherHotkey))
            {
                ErrorText = $"Эта комбинация уже назначена на {OtherHotkeyName}";
                return;
            }

            // Свои хоткеи на время диалога сняты, поэтому проверка честная
            if (!GlobalHotkeyService.IsAvailable(hotkey))
            {
                ErrorText = "Комбинация уже занята другой программой или Windows";
                return;
            }

            CloseRequested?.Invoke(hotkey);
        }

        public void Cancel() => CloseRequested?.Invoke(null);

        // Метод для обновления комбинации из события KeyDown
        public void UpdateHotkeyFromKeyDown(object? sender, KeyEventArgs e)
        {
            var hotkey = GetStringHotkeyKeyDown(e);
            if (hotkey != null)
            {
                CurrentHotkey = hotkey;
                ErrorText = null;
            }
        }

        /// <summary>Боковые / средняя кнопки мыши: "Mouse4", "Mouse5", "MouseMiddle".</summary>
        public void UpdateHotkeyFromMouse(KeyModifiers modifiers, string button)
        {
            CurrentHotkey = ModifiersPrefix(modifiers) + button;
            ErrorText = null;
        }

        private static string ModifiersPrefix(KeyModifiers modifiers)
        {
            var prefix = "";
            if (modifiers.HasFlag(KeyModifiers.Control))
                prefix += "Ctrl+";
            if (modifiers.HasFlag(KeyModifiers.Shift))
                prefix += "Shift+";
            if (modifiers.HasFlag(KeyModifiers.Alt))
                prefix += "Alt+";
            return prefix;
        }

        private static string? GetStringHotkeyKeyDown(KeyEventArgs e)
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

            var hotkey = ModifiersPrefix(modifiers) + key.ToString();
            e.Handled = true;
            return hotkey;
        }
    }
}
