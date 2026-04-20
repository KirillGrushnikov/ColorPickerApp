using Avalonia.Media;
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
        private bool _autoCopyOnPick = true;
        public bool AutoCopyOnPick
        {
            get => _autoCopyOnPick;
            set => this.RaiseAndSetIfChanged(ref _autoCopyOnPick, value);
        }

        private string _pickerHotkey = "Ctrl+Shift+P";
        public string PickerHotkey
        {
            get => _pickerHotkey;
            set => this.RaiseAndSetIfChanged(ref _pickerHotkey, value);
        }

        private bool _restoreWindowAfterPick = true;
        public bool RestoreWindowAfterPick
        {
            get => _restoreWindowAfterPick;
            set => this.RaiseAndSetIfChanged(ref _restoreWindowAfterPick, value);
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
                BackgroundType = (TransparentBackgroundType)value;
                IsCustomColorSelected = BackgroundType == TransparentBackgroundType.CustomColor;
            }
        }

        private Color _customBackgroundColor = Colors.White;
        public Color CustomBackgroundColor
        {
            get => _customBackgroundColor;
            set => this.RaiseAndSetIfChanged(ref _customBackgroundColor, value);
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
        }

        private void ResetToDefaults()
        {
            AutoCopyOnPick = true;
            PickerHotkey = "Ctrl+Shift+P";
            RestoreWindowAfterPick = true;
            BackgroundTypeIndex = 0;
            CustomBackgroundColor = Colors.White;
        }
    }
}