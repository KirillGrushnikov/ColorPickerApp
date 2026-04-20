using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using ColorPickerApp.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

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
        private bool _permanentCloseWindow = true;
        public bool PermanentCloseWindow
        {
            get => _permanentCloseWindow;
            set
            {
                this.RaiseAndSetIfChanged(ref _permanentCloseWindow, value);
                App.Settings.PermanentCloseWindow = value;
                App.Settings.Save();
            }
        }

        private bool _autoCopyOnPick = true;
        public bool AutoCopyOnPick
        {
            get => _autoCopyOnPick;
            set
            {
                this.RaiseAndSetIfChanged(ref _autoCopyOnPick, value);
                App.Settings.AutoCopyOnPick = value;
                App.Settings.Save();
            }
        }

        private string _pickerHotkey = "Ctrl+Shift+P";
        public string PickerHotkey
        {
            get => _pickerHotkey;
            set
            {
                this.RaiseAndSetIfChanged(ref _pickerHotkey, value);
                App.Settings.PickerHotkey = value;
                App.Settings.Save();
            }
        }

        private string _openWindowHotkey = "Ctrl+Shift+O";
        public string OpenWindowHotkey
        {
            get => _openWindowHotkey;
            set
            {
                this.RaiseAndSetIfChanged(ref _openWindowHotkey, value);
                App.Settings.OpenWindowHotkey = value;
                App.Settings.Save();
            }
        }

        private bool _restoreWindowAfterPick = true;
        public bool RestoreWindowAfterPick
        {
            get => _restoreWindowAfterPick;
            set
            {
                this.RaiseAndSetIfChanged(ref _restoreWindowAfterPick, value);
                App.Settings.RestoreWindowAfterPick = value;
                App.Settings.Save();
            }
        }

        private TransparentBackgroundType _backgroundType = TransparentBackgroundType.LightCheckerboard;
        public TransparentBackgroundType BackgroundType
        {
            get => _backgroundType;
            set => this.RaiseAndSetIfChanged(ref _backgroundType, value);
        }

        private int _backgroundTypeIndex = 0;

        public int BackgroundTypeIndex
        {
            get => _backgroundTypeIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _backgroundTypeIndex, value);
                App.Settings.TransparentBackgroundIndex = _backgroundTypeIndex;
                BackgroundType = (TransparentBackgroundType)value;
                IsCustomColorSelected = BackgroundType == TransparentBackgroundType.CustomColor;
                UpdateTransparentBackground();
            }
        }

        private Color _customBackgroundColor = Colors.White;
        public Color CustomBackgroundColor
        {
            get => _customBackgroundColor;
            set
            {
                this.RaiseAndSetIfChanged(ref _customBackgroundColor, value);
                UpdateTransparentBackground();
            }
        }

        private bool _isCustomColorSelected = false;
        public bool IsCustomColorSelected
        {
            get => _isCustomColorSelected;
            set => this.RaiseAndSetIfChanged(ref _isCustomColorSelected, value);
        }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ResetCommand { get; }

        public SettingsViewModel()
        {
            ResetCommand = ReactiveCommand.Create(ResetToDefaults);
            IsCustomColorSelected = BackgroundType == TransparentBackgroundType.CustomColor;
            PermanentCloseWindow = App.Settings.PermanentCloseWindow;
            AutoCopyOnPick = App.Settings.AutoCopyOnPick;
            PickerHotkey = App.Settings.PickerHotkey;
            OpenWindowHotkey = App.Settings.OpenWindowHotkey;
            RestoreWindowAfterPick = App.Settings.RestoreWindowAfterPick;
            CustomBackgroundColor = App.Settings.TransparentBackground1.Color;
            BackgroundTypeIndex = App.Settings.TransparentBackgroundIndex;
        }

        public async void ButtonPickerHotkey_OnClick()
        {
            var dialog = new HotkeyDialogView();
            var viewmodel = new HotkeyDialogViewModel();
            dialog.DataContext = viewmodel;
            // Устанавливаем текущую горячую клавишу в диалог (если нужно показать существующую)
            viewmodel.CurrentHotkey = PickerHotkey;

            var result = await dialog.ShowDialog<string?>(GetMainWindow());
            if (result != null)
            {
                PickerHotkey = result;
            }
        }
        public async void ButtonOpenWindowHotkey_OnClick()
        {
            var dialog = new HotkeyDialogView();
            var viewmodel = new HotkeyDialogViewModel();
            dialog.DataContext = viewmodel;
            // Устанавливаем текущую горячую клавишу в диалог (если нужно показать существующую)
            viewmodel.CurrentHotkey = PickerHotkey;

            var result = await dialog.ShowDialog<string?>(GetMainWindow());
            if (result != null)
            {
                OpenWindowHotkey = result;
            }
        }

        private Window GetMainWindow()
        {
            // Получаем главное окно из Application.Current
            return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                   ?? throw new InvalidOperationException("No main window found");
        }

        private void ResetToDefaults()
        {
            PermanentCloseWindow = false;
            AutoCopyOnPick = false;
            PickerHotkey = "Ctrl+Shift+P";
            OpenWindowHotkey = "Ctrl+Shift+O";
            RestoreWindowAfterPick = false;
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