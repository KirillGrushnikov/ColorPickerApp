using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Globalization;
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
        public bool AlwaysOnTop
        {
            get => App.Settings.AlwaysOnTop;
            set
            {
                if (App.Settings.AlwaysOnTop == value)
                    return;
                App.Settings.AlwaysOnTop = value; // окно подписано на изменение и сразу применит
                App.Settings.Save();
                this.RaisePropertyChanged();
            }
        }

        public bool RememberLastColor
        {
            get => App.Settings.RememberLastColor;
            set
            {
                if (App.Settings.RememberLastColor == value)
                    return;
                App.Settings.RememberLastColor = value;
                App.Settings.Save();
                this.RaisePropertyChanged();
            }
        }

        public bool HexUppercase
        {
            get => App.Settings.HexUppercase;
            set
            {
                if (App.Settings.HexUppercase == value)
                    return;
                App.Settings.HexUppercase = value;
                App.Settings.Save();
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(CustomBackgroundHex));
            }
        }

        private readonly string _startupLanguage = App.Settings.Language;

        /// <summary>0 - как в Windows, 1 - русский, 2 - English.</summary>
        public int LanguageIndex
        {
            get => App.Settings.Language switch
            {
                Loc.Russian => 1,
                Loc.English => 2,
                _ => 0,
            };
            set
            {
                var language = value switch
                {
                    1 => Loc.Russian,
                    2 => Loc.English,
                    _ => "",
                };
                if (App.Settings.Language == language)
                    return;
                App.Settings.Language = language;
                App.Settings.Save();
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsRestartNeeded));
            }
        }

        /// <summary>Язык выбран, но ещё не применён - показываем кнопку «Перезапустить».</summary>
        public bool IsRestartNeeded => App.Settings.Language != _startupLanguage;

        public void RestartApp() => App.Restart();

        /// <summary>0 - #RRGGBBAA (CSS), 1 - #AARRGGBB (Android, XAML).</summary>
        public int HexAlphaOrderIndex
        {
            get => App.Settings.HexAlphaFirst ? 1 : 0;
            set
            {
                var alphaFirst = value == 1;
                if (App.Settings.HexAlphaFirst == alphaFirst)
                    return;
                App.Settings.HexAlphaFirst = alphaFirst;
                App.Settings.Save();
                this.RaisePropertyChanged();
            }
        }

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
                if (!_customFromHsv)
                    SyncCustomHsv();
                this.RaisePropertyChanged(nameof(CustomBackgroundHex));
                this.RaisePropertyChanged(nameof(CustomBackgroundBrush));
                UpdateTransparentBackground();
            }
        }

        #region Свой цвет фона (круг HSV во всплывающем окне)

        // HSV хранится отдельно, чтобы тон не сбрасывался у серых и чёрного
        private double _customHue;
        private double _customSaturation;
        private double _customValue = 1;
        private bool _customFromHsv;

        public double CustomHue
        {
            get => _customHue;
            set
            {
                if (Math.Abs(value - _customHue) < 1e-9)
                    return;
                _customHue = value;
                this.RaisePropertyChanged();
                ApplyCustomHsv();
            }
        }

        public double CustomSaturation
        {
            get => _customSaturation;
            set
            {
                if (Math.Abs(value - _customSaturation) < 1e-9)
                    return;
                _customSaturation = value;
                this.RaisePropertyChanged();
                ApplyCustomHsv();
            }
        }

        public double CustomValue
        {
            get => _customValue;
            set
            {
                if (Math.Abs(value - _customValue) < 1e-9)
                    return;
                _customValue = value;
                this.RaisePropertyChanged();
                ApplyCustomHsv();
            }
        }

        public string CustomBackgroundHex
        {
            get => ColorFormat.Hex(_customBackgroundColor);
            set
            {
                var hex = value?.Trim().TrimStart('#') ?? "";
                if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                    CustomBackgroundColor = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }
        }

        public IBrush CustomBackgroundBrush => new ImmutableSolidColorBrush(_customBackgroundColor);

        /// <summary>Взять текущий цвет из главного окна.</summary>
        public void UseCurrentColorForBackground()
        {
            if (App.MainViewModel is { } main)
                CustomBackgroundColor = main.OpaqueColor;
        }

        private void ApplyCustomHsv()
        {
            _customFromHsv = true;
            try
            {
                CustomBackgroundColor = ColorMath.FromHsv(_customHue, _customSaturation, _customValue);
            }
            finally
            {
                _customFromHsv = false;
            }
        }

        private void SyncCustomHsv()
        {
            var (h, s, v) = ColorMath.ToHsv(_customBackgroundColor);
            if (v > 0)
            {
                if (s > 0)
                    _customHue = h;
                _customSaturation = s;
            }
            _customValue = v;

            this.RaisePropertyChanged(nameof(CustomHue));
            this.RaisePropertyChanged(nameof(CustomSaturation));
            this.RaisePropertyChanged(nameof(CustomValue));
        }

        #endregion

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
            SyncCustomHsv();

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
            var result = await AskHotkeyAsync(PickerHotkey, OpenWindowHotkey, Loc.T("hk_other_open"));
            if (result != null)
                PickerHotkey = result;
        }

        public async void ButtonOpenWindowHotkey_OnClick()
        {
            var result = await AskHotkeyAsync(OpenWindowHotkey, PickerHotkey, Loc.T("hk_other_picker"));
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
            HexAlphaOrderIndex = 0;
            HexUppercase = false;
            AlwaysOnTop = false;
            RememberLastColor = true;
            BackgroundTypeIndex = 0;
            CustomBackgroundColor = Colors.White;
            App.Settings.SetLayout(WindowLayout.Classic);
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
