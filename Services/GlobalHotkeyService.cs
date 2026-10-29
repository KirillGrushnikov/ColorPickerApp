using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorPickerApp.Services;

public enum HotkeyStatus
{
    /// <summary>Хоткей не задан.</summary>
    NotSet,
    /// <summary>Зарегистрирован и работает.</summary>
    Registered,
    /// <summary>Комбинация уже занята другой программой или Windows.</summary>
    Busy,
    /// <summary>Клавиша не поддерживается.</summary>
    Invalid,
    /// <summary>Совпадает с другим хоткеем этого приложения.</summary>
    Duplicate,
    /// <summary>Временно отключён (открыт диалог назначения клавиши).</summary>
    Suspended,
    /// <summary>Не удалось включить перехват кнопок мыши.</summary>
    HookFailed,
}

/// <summary>
/// Глобальные горячие клавиши через Win32 RegisterHotKey.
/// В отличие от низкоуровневого хука (SharpHook/uiohook) Windows сама сообщает только о нужной
/// комбинации - остальные нажатия в системе вообще не проходят через приложение. Плюс сразу видно,
/// если комбинация уже занята другой программой: RegisterHotKey вернёт ошибку.
///
/// Хоткеи регистрируются на отдельном фоновом потоке с собственной очередью сообщений:
/// WM_HOTKEY приходит в неё без окна, и не нужно вмешиваться в цикл сообщений Avalonia.
///
/// Кнопки мыши (боковые Mouse4/Mouse5 и средняя) RegisterHotKey не поддерживает, поэтому для них
/// в том же потоке ставится низкоуровневый хук мыши WH_MOUSE_LL - только если такой хоткей назначен.
/// Совпавшее нажатие не передаётся дальше (иначе «Назад» ещё и листал бы браузер).
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int PickerId = 1;
    private const int OpenWindowId = 2;
    private const int ProbeId = 0xBF00;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000; // удержание клавиши не повторяет срабатывание

    private const uint WM_QUIT = 0x0012;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_APP_APPLY = 0x8000 + 1;
    private const uint PM_NOREMOVE = 0x0000;

    private const int WH_MOUSE_LL = 14;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_XBUTTONDOWN = 0x020B;
    private const uint WM_XBUTTONUP = 0x020C;

    // Виртуальные коды кнопок мыши (так они хранятся после разбора строки хоткея)
    private const uint VK_MBUTTON = 0x04;
    private const uint VK_XBUTTON1 = 0x05;
    private const uint VK_XBUTTON2 = 0x06;

    // Имена клавиш Avalonia (как они сохраняются в настройках) -> виртуальные коды Windows
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Enter"] = 0x0D, ["Return"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Tab"] = 0x09,
        ["Back"] = 0x08, ["Backspace"] = 0x08,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21, ["Prior"] = 0x21,
        ["PageDown"] = 0x22, ["Next"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["PrintScreen"] = 0x2C, ["Snapshot"] = 0x2C,
        ["Pause"] = 0x13,
        ["Scroll"] = 0x91,
        ["Multiply"] = 0x6A,
        ["Add"] = 0x6B,
        ["Subtract"] = 0x6D,
        ["Decimal"] = 0x6E,
        ["Divide"] = 0x6F,
        ["Oem1"] = 0xBA, ["OemSemicolon"] = 0xBA,
        ["OemPlus"] = 0xBB,
        ["OemComma"] = 0xBC,
        ["OemMinus"] = 0xBD,
        ["OemPeriod"] = 0xBE,
        ["Oem2"] = 0xBF, ["OemQuestion"] = 0xBF,
        ["Oem3"] = 0xC0, ["OemTilde"] = 0xC0,
        ["Oem4"] = 0xDB, ["OemOpenBrackets"] = 0xDB,
        ["Oem5"] = 0xDC, ["OemPipe"] = 0xDC,
        ["Oem6"] = 0xDD, ["OemCloseBrackets"] = 0xDD,
        ["Oem7"] = 0xDE, ["OemQuotes"] = 0xDE,
        ["Oem102"] = 0xE2, ["OemBackslash"] = 0xE2,

        // Кнопки мыши
        ["Mouse4"] = VK_XBUTTON1, ["XButton1"] = VK_XBUTTON1,
        ["Mouse5"] = VK_XBUTTON2, ["XButton2"] = VK_XBUTTON2,
        ["MouseMiddle"] = VK_MBUTTON, ["MiddleButton"] = VK_MBUTTON,
    };

    // Хук мыши вызывается Windows как статическая функция - состояние для него статическое
    private static GlobalHotkeyService? s_instance;
    private static volatile int s_mousePickerCombo;   // (MOD_* << 8) | VK, 0 - нет
    private static volatile int s_mouseOpenWindowCombo;
    private static uint s_suppressButtonUp;           // отпускание какой кнопки тоже проглотить
    private IntPtr _mouseHook;

    private Thread? _thread;
    private volatile uint _threadId;
    private volatile string? _pickerHotkey;
    private volatile string? _openWindowHotkey;
    private volatile bool _suspended;
    private volatile HotkeyStatus _pickerStatus = HotkeyStatus.NotSet;
    private volatile HotkeyStatus _openWindowStatus = HotkeyStatus.NotSet;
    private bool _disposed;

    /// <summary>Вызываются в потоке хоткеев - обработчик должен сам перейти в UI-поток.</summary>
    public event Action? PickerPressed;
    public event Action? OpenWindowPressed;
    /// <summary>Изменился статус регистрации (тоже из потока хоткеев).</summary>
    public event Action? StatusChanged;

    public HotkeyStatus PickerStatus => _pickerStatus;
    public HotkeyStatus OpenWindowStatus => _openWindowStatus;

    /// <summary>
    /// Временно снять регистрацию (пока открыт диалог назначения клавиши) - иначе Windows
    /// перехватит комбинацию и диалог её не увидит.
    /// </summary>
    public bool IsSuspended
    {
        get => _suspended;
        set
        {
            if (_suspended == value)
                return;
            _suspended = value;
            RequestApply();
        }
    }

    public GlobalHotkeyService()
    {
        s_instance = this;
        SnapshotSettings();
        App.Settings.PropertyChanged += OnSettingsChanged;
    }

    public void Start()
    {
        if (_thread != null || _disposed || !OperatingSystem.IsWindows())
            return;

        _thread = new Thread(ThreadProc) { IsBackground = true, Name = "Global hotkeys" };
        _thread.Start();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettingsStore.PickerHotkey) or nameof(AppSettingsStore.OpenWindowHotkey))
        {
            SnapshotSettings();
            RequestApply();
        }
    }

    private void SnapshotSettings()
    {
        _pickerHotkey = App.Settings.PickerHotkey;
        _openWindowHotkey = App.Settings.OpenWindowHotkey;
    }

    private void RequestApply()
    {
        // Если поток ещё не создал очередь, он сам применит актуальные настройки при старте
        var threadId = _threadId;
        if (threadId != 0)
            PostThreadMessageW(threadId, WM_APP_APPLY, IntPtr.Zero, IntPtr.Zero);
    }

    private unsafe void ThreadProc()
    {
        MSG msg;
        PeekMessageW(&msg, IntPtr.Zero, 0, 0, PM_NOREMOVE); // создаёт очередь сообщений потока
        _threadId = GetCurrentThreadId();
        Apply();

        while (GetMessageW(&msg, IntPtr.Zero, 0, 0) > 0)
        {
            try
            {
                if (msg.message == WM_HOTKEY)
                {
                    var id = (int)msg.wParam;
                    if (id == PickerId)
                        PickerPressed?.Invoke();
                    else if (id == OpenWindowId)
                        OpenWindowPressed?.Invoke();
                }
                else if (msg.message == WM_APP_APPLY)
                {
                    Apply();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
            }
        }

        UnregisterHotKey(IntPtr.Zero, PickerId);
        UnregisterHotKey(IntPtr.Zero, OpenWindowId);
        SetMouseHook(false);
    }

    // Только в потоке хоткеев: RegisterHotKey(NULL, ...) привязывает хоткей к вызывающему потоку
    private void Apply()
    {
        UnregisterHotKey(IntPtr.Zero, PickerId);
        UnregisterHotKey(IntPtr.Zero, OpenWindowId);
        s_mousePickerCombo = 0;
        s_mouseOpenWindowCombo = 0;

        if (_suspended)
        {
            _pickerStatus = HotkeyStatus.Suspended;
            _openWindowStatus = HotkeyStatus.Suspended;
        }
        else
        {
            var picker = _pickerHotkey;
            var openWindow = _openWindowHotkey;
            _pickerStatus = Register(PickerId, picker);
            _openWindowStatus = !string.IsNullOrWhiteSpace(openWindow) && AreSame(picker, openWindow)
                ? HotkeyStatus.Duplicate
                : Register(OpenWindowId, openWindow);
        }

        // Хук мыши нужен, только если хоть один хоткей назначен на кнопку мыши
        var needMouseHook = s_mousePickerCombo != 0 || s_mouseOpenWindowCombo != 0;
        if (!SetMouseHook(needMouseHook) && needMouseHook)
        {
            if (s_mousePickerCombo != 0) _pickerStatus = HotkeyStatus.HookFailed;
            if (s_mouseOpenWindowCombo != 0) _openWindowStatus = HotkeyStatus.HookFailed;
        }

        StatusChanged?.Invoke();
    }

    private static HotkeyStatus Register(int id, string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return HotkeyStatus.NotSet;
        if (!TryParse(hotkey, out var modifiers, out var vk))
            return HotkeyStatus.Invalid;

        if (IsMouseButton(vk))
        {
            var combo = (int)((modifiers << 8) | vk);
            if (id == PickerId) s_mousePickerCombo = combo;
            else s_mouseOpenWindowCombo = combo;
            return HotkeyStatus.Registered;
        }

        return RegisterHotKey(IntPtr.Zero, id, modifiers | MOD_NOREPEAT, vk) != 0
            ? HotkeyStatus.Registered
            : HotkeyStatus.Busy;
    }

    #region Хук мыши

    private static bool IsMouseButton(uint vk) => vk is VK_MBUTTON or VK_XBUTTON1 or VK_XBUTTON2;

    /// <summary>Ставит или снимает хук мыши; true - состояние соответствует запрошенному.</summary>
    private unsafe bool SetMouseHook(bool enable)
    {
        if (enable == (_mouseHook != IntPtr.Zero))
            return true;

        if (!enable)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            s_suppressButtonUp = 0;
            return true;
        }

        delegate* unmanaged<int, IntPtr, IntPtr, IntPtr> proc = &MouseHookProc;
        _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, (IntPtr)(void*)proc, GetModuleHandleW(IntPtr.Zero), 0);
        return _mouseHook != IntPtr.Zero;
    }

    // Вызывается Windows в потоке хоткеев на каждое событие мыши - только проверка чисел, без аллокаций.
    // Совпавшее нажатие (и его отпускание) проглатывается, остальное идёт дальше без изменений.
    [UnmanagedCallersOnly]
    private static unsafe IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = (uint)wParam;
                uint vk = 0;
                var isDown = false;
                switch (message)
                {
                    case WM_MBUTTONDOWN:
                        vk = VK_MBUTTON;
                        isDown = true;
                        break;
                    case WM_MBUTTONUP:
                        vk = VK_MBUTTON;
                        break;
                    case WM_XBUTTONDOWN:
                    case WM_XBUTTONUP:
                        var button = ((MSLLHOOKSTRUCT*)lParam)->mouseData >> 16;
                        vk = button == 1 ? VK_XBUTTON1 : button == 2 ? VK_XBUTTON2 : 0;
                        isDown = message == WM_XBUTTONDOWN;
                        break;
                }

                if (vk != 0)
                {
                    var instance = s_instance;
                    if (isDown && instance is { _suspended: false })
                    {
                        var combo = (int)((CurrentModifiers() << 8) | vk);
                        if (combo == s_mousePickerCombo)
                        {
                            s_suppressButtonUp = vk;
                            instance.PickerPressed?.Invoke();
                            return (IntPtr)1;
                        }
                        if (combo == s_mouseOpenWindowCombo)
                        {
                            s_suppressButtonUp = vk;
                            instance.OpenWindowPressed?.Invoke();
                            return (IntPtr)1;
                        }
                    }
                    else if (!isDown && s_suppressButtonUp == vk)
                    {
                        s_suppressButtonUp = 0;
                        return (IntPtr)1;
                    }
                }
            }
        }
        catch
        {
            // Исключение не должно уйти в Windows
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static uint CurrentModifiers()
    {
        uint modifiers = 0;
        if (IsKeyDown(0x11)) modifiers |= MOD_CONTROL; // VK_CONTROL
        if (IsKeyDown(0x10)) modifiers |= MOD_SHIFT;   // VK_SHIFT
        if (IsKeyDown(0x12)) modifiers |= MOD_ALT;     // VK_MENU
        if (IsKeyDown(0x5B) || IsKeyDown(0x5C)) modifiers |= MOD_WIN;
        return modifiers;
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    #endregion

    #region Разбор и проверка комбинаций

    /// <summary>"Ctrl+Shift+P" -> модификаторы MOD_* и виртуальный код клавиши.</summary>
    public static bool TryParse(string? hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkey))
            return false;

        string? keyName = null;
        foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "win":
                case "meta":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (keyName != null)
                        return false; // две обычные клавиши в одной комбинации
                    keyName = part;
                    break;
            }
        }

        return keyName != null && TryGetVirtualKey(keyName, out virtualKey);
    }

    private static bool TryGetVirtualKey(string name, out uint vk)
    {
        vk = 0;

        // A..Z, 0..9
        if (name.Length == 1)
        {
            var c = char.ToUpperInvariant(name[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
        }

        // Avalonia: D0..D9
        if (name.Length == 2 && (name[0] == 'D' || name[0] == 'd') && char.IsDigit(name[1]))
        {
            vk = (uint)name[1];
            return true;
        }

        // NumPad0..NumPad9
        if (name.Length == 7 && name.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && char.IsDigit(name[6]))
        {
            vk = 0x60u + (uint)(name[6] - '0');
            return true;
        }

        // F1..F24
        if (name.Length is 2 or 3 && (name[0] == 'F' || name[0] == 'f')
            && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var f)
            && f is >= 1 and <= 24)
        {
            vk = 0x6Fu + (uint)f; // VK_F1 = 0x70
            return true;
        }

        return NamedKeys.TryGetValue(name, out vk);
    }

    /// <summary>Одна и та же комбинация, даже если записана по-разному ("Ctrl+D1" и "control+1").</summary>
    public static bool AreSame(string? a, string? b)
    {
        if (TryParse(a, out var modsA, out var vkA) && TryParse(b, out var modsB, out var vkB))
            return modsA == modsB && vkA == vkB;
        return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Текст ошибки, если комбинацию нельзя использовать как глобальную; иначе null.</summary>
    public static string? Validate(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return "Нажмите комбинацию клавиш";
        if (!TryParse(hotkey, out var modifiers, out var vk))
            return "Эта клавиша не поддерживается для глобального хоткея";

        // Без Ctrl/Alt/Win обычная клавиша (или Shift+буква) перестанет печататься во всех программах
        var isStandalone = vk is >= 0x70 and <= 0x87 // F1..F24
                           || vk is 0x2C or 0x13 or 0x91 // PrintScreen, Pause, ScrollLock
                           || vk is VK_XBUTTON1 or VK_XBUTTON2; // боковые кнопки мыши
        if ((modifiers & (MOD_CONTROL | MOD_ALT | MOD_WIN)) == 0 && !isStandalone)
            return vk == VK_MBUTTON
                ? "Средняя кнопка мыши - только с Ctrl или Alt: иначе она перестанет работать в других программах"
                : "Добавьте Ctrl или Alt: иначе эта клавиша перестанет работать в других программах";

        return null;
    }

    /// <summary>
    /// Свободна ли комбинация в системе: пробная регистрация и сразу снятие.
    /// Собственные хоткеи приложения на время проверки должны быть сняты (IsSuspended).
    /// </summary>
    public static bool IsAvailable(string hotkey)
    {
        if (!OperatingSystem.IsWindows() || !TryParse(hotkey, out var modifiers, out var vk))
            return false;
        if (IsMouseButton(vk))
            return true; // занятость кнопок мыши другими программами проверить нельзя
        if (RegisterHotKey(IntPtr.Zero, ProbeId, modifiers | MOD_NOREPEAT, vk) == 0)
            return false;
        UnregisterHotKey(IntPtr.Zero, ProbeId);
        return true;
    }

    /// <summary>Текст для интерфейса; пустая строка - всё в порядке.</summary>
    public static string Describe(HotkeyStatus status) => status switch
    {
        HotkeyStatus.Busy => "⚠ Комбинация занята другой программой - хоткей не работает",
        HotkeyStatus.Invalid => "⚠ Клавиша не поддерживается - назначьте другую",
        HotkeyStatus.Duplicate => "⚠ Совпадает с другим хоткеем - назначьте другую",
        HotkeyStatus.HookFailed => "⚠ Не удалось включить перехват кнопок мыши",
        _ => "",
    };

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        App.Settings.PropertyChanged -= OnSettingsChanged;

        var threadId = _threadId;
        if (threadId != 0)
        {
            PostThreadMessageW(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread?.Join(500);
        }
    }

    #region Win32

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll")]
    private static extern int RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern int UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern unsafe int GetMessageW(MSG* lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern unsafe int PeekMessageW(MSG* lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern int PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int ptX;
        public int ptY;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookExW(int idHook, IntPtr lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern int UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(IntPtr lpModuleName);

    #endregion
}
