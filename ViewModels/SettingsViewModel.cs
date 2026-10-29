using Avalonia.Media;
using Avalonia.Threading;
using ColorPickerApp.Services;
using ColorPickerApp.Views;
using System;
using System.Threading.Tasks;

namespace ColorPickerApp.ViewModels
{
    public enum TransparentBackgroundType
    {
        LightCheckerboard,
        DarkCheckerboard,
        CustomColor
    }

    public class SettingsViewModel : ViewModelBase
    {
        private bool _autoStart;
        public bool IsAutoStart
        {
            get => _autoStart;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _autoStart, value))
                    return;

                App.Settings.IsAutoStart = value;
                if (value)
                {
                    PermanentCloseWindow = false;
                    StartupManager.Enable();
                }
                else
                {
                    StartupManager.Disable();
                }

                App.Settings.Save();
            }
        }

        private bool _permanentCloseWindow;
        public bool PermanentCloseWindow
        {
            get => _permanentCloseWindow;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _permanentCloseWindow, value))
                    return;
                App.Settings.PermanentCloseWindow = value;
                App.Settings.Save();
            }
        }

        private bool _autoCopyOnPick;
        public bool AutoCopyOnPick
        {
            get => _autoCopyOnPick;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _autoCopyOnPick, value))
                    return;
                App.Settings.AutoCopyOnPick = value;
                App.Settings.Save();
            }
        }

        private string _pickerHotkey;
        public string PickerHotkey
        {
            get => _pickerHotkey;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _pickerHotkey, value))
                    return;
                App.Settings.PickerHotkey = value;
                App.Settings.Save();
            }
        }

        private string _openWindowHotkey;
        public string OpenWindowHotkey
        {
            get => _openWindowHotkey;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _openWindowHotkey, value))
                    return;
                App.Settings.OpenWindowHotkey = value;
                App.Settings.Save();
            }
        }

        private bool _restoreWindowAfterPick;
        public bool RestoreWindowAfterPick
        {
            get => _restoreWindowAfterPick;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _restoreWindowAfterPick, value))
                    return;
                App.Settings.RestoreWindowAfterPick = value;
                App.Settings.Save();
            }
        }

        private TransparentBackgroundType _backgroundType;
        public TransparentBackgroundType BackgroundType
        {
            get => _backgroundType;
            set => this.RaiseAndSetIfChanged(ref _backgroundType, value);
        }

        private int _backgroundTypeIndex;
        public int BackgroundTypeIndex
        {
            get => _backgroundTypeIndex;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _backgroundTypeIndex, value))
                    return;
                App.Settings.TransparentBackgroundIndex = value;
                BackgroundType = (TransparentBackgroundType)value;
                IsCustomColorSelected = BackgroundType == TransparentBackgroundType.CustomColor;
                UpdateTransparentBackground();
            }
        }

        private Color _customBackgroundColor;
        public Color CustomBackgroundColor
        {
            get => _customBackgroundColor;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _customBackgroundColor, value))
                    return;
                UpdateTransparentBackground();
            }
        }

        private bool _isCustomColorSelected;
        public bool IsCustomColorSelected
        {
            get => _isCustomColorSelected;
            set => this.RaiseAndSetIfChanged(ref _isCustomColorSelected, value);
        }

        public SettingsViewModel()
        {
            // Читаем значения напрямую в поля: раньше каждое присваивание через свойство
            // вызывало App.Settings.Save(), т.е. ~7 записей settings.json при открытии окна.
            var s = App.Settings;
            _autoStart = s.IsAutoStart;
            _permanentCloseWindow = s.PermanentCloseWindow;
            _autoCopyOnPick = s.AutoCopyOnPick;
            _pickerHotkey = s.PickerHotkey;
            _openWindowHotkey = s.OpenWindowHotkey;
            _restoreWindowAfterPick = s.RestoreWindowAfterPick;
            _backgroundTypeIndex = Math.Clamp(s.TransparentBackgroundIndex, 0, 2);
            _backgroundType = (TransparentBackgroundType)_backgroundTypeIndex;
            _isCustomColorSelected = _backgroundType == TransparentBackgroundType.CustomColor;
            _customBackgroundColor = s.TransparentBackground1.Color;

            if (App.Hotkeys is { } hotkeys)
                hotkeys.StatusChanged += () => Dispatcher.UIThread.Post(RaiseHotkeyStatus);
        }

        /// <summary>Пусто - хоткей работает; иначе причина (занят другой программой и т.п.).</summary>
        public string PickerHotkeyStatus =>
            GlobalHotkeyService.Describe(App.Hotkeys?.PickerStatus ?? HotkeyStatus.NotSet);

        public string OpenWindowHotkeyStatus =>
            GlobalHotkeyService.Describe(App.Hotkeys?.OpenWindowStatus ?? HotkeyStatus.NotSet);

        private void RaiseHotkeyStatus()
        {
            this.RaisePropertyChanged(nameof(PickerHotkeyStatus));
            this.RaisePropertyChanged(nameof(OpenWindowHotkeyStatus));
        }

        public async void ButtonPickerHotkey_OnClick()
        {
            var result = await AskHotkeyAsync(PickerHotkey, OpenWindowHotkey, "открытие окна");
            if (result != null)
                PickerHotkey = result;
        }

        public async void ButtonOpenWindowHotkey_OnClick()
        {
            var result = await AskHotkeyAsync(OpenWindowHotkey, PickerHotkey, "пипетку");
            if (result != null)
                OpenWindowHotkey = result;
        }

        private static Task<string?> AskHotkeyAsync(string currentHotkey, string otherHotkey, string otherHotkeyName)
        {
            var viewModel = new HotkeyDialogViewModel
            {
                CurrentHotkey = currentHotkey,
                OtherHotkey = otherHotkey,
                OtherHotkeyName = otherHotkeyName,
            };
            var dialog = new HotkeyDialogView { DataContext = viewModel };
            return dialog.ShowDialog<string?>(App.GetOrCreateMainWindow());
        }

        public void ResetToDefaults()
        {
            IsAutoStart = false;
            PermanentCloseWindow = false;
            AutoCopyOnPick = false;
            PickerHotkey = "Ctrl+Shift+P";
            OpenWindowHotkey = "Ctrl+Shift+O";
            RestoreWindowAfterPick = true;
            BackgroundTypeIndex = 0;
            CustomBackgroundColor = Colors.White;
        }

        private void UpdateTransparentBackground()
        {
            switch (BackgroundType)
            {
                case TransparentBackgroundType.LightCheckerboard:
                    App.Settings.TransparentColor1 = "#ffffff";
                    App.Settings.TransparentColor2 = "#c1c1c1";
                    break;
                case TransparentBackgroundType.DarkCheckerboard:
                    App.Settings.TransparentColor1 = "#2b2b2b";
                    App.Settings.TransparentColor2 = "#333333";
                    break;
                case TransparentBackgroundType.CustomColor:
                    App.Settings.TransparentColor1 = ColorMath.ToHex(CustomBackgroundColor);
                    App.Settings.TransparentColor2 = ColorMath.ToHex(CustomBackgroundColor);
                    break;
            }

            App.Settings.Save();
        }
    }
}
