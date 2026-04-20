
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using SharpHook.Reactive;
using System;
using System.Reactive.Linq;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace ColorPickerApp.Services;

public class GlobalHotkeyService : ReactiveObject, IDisposable
{
    private readonly ReactiveGlobalHook _hook;
    private readonly IDisposable _subscription;

    // Событие, которое будет вызвано при нажатии нашей глобальной комбинации
    public event Action? HotkeyOpenAppPressed;
    public event Action? HotkeyOpenPipetePressed;

    public GlobalHotkeyService()
    {
        // Создаём экземпляр глобального хука
        _hook = new ReactiveGlobalHook();
        _hook.KeyPressed.Subscribe(OnHotkeyCombination);
    }

    // Метод для запуска прослушивания
    public void Start()
    {
        if (!_hook.IsRunning)
        {
            _hook.RunAsync(); // Запускаем хук в фоновом потоке
        }
    }

    // Метод для остановки прослушивания
    public void Stop()
    {
        if (_hook.IsRunning)
        {
            _hook.Stop();
        }
    }

    // Логика проверки нужной комбинации клавиш
    // Здесь вы можете задать любую комбинацию, например, Ctrl + Shift + P
    private void OnHotkeyCombination(KeyboardHookEventArgs e)
    {
        string? hotkey = GetCombination(e);
        if (hotkey == null) return;

        if (App.Settings.PickerHotkey == hotkey)
        {
            HotkeyOpenPipetePressed?.Invoke();
            return;
        }

        if (App.Settings.OpenWindowHotkey == hotkey)
        {
            HotkeyOpenAppPressed?.Invoke();
            return;
        }
    }

    private string? GetCombination(KeyboardHookEventArgs e)
    {
        var rawEvent = e.RawEvent;

        // Игнорируем одиночные клавиши-модификаторы
        bool isCtrlPressed = (rawEvent.Mask & EventMask.Ctrl) != 0;
        bool isShiftPressed = (rawEvent.Mask & EventMask.Shift) != 0;
        bool isAltPressed = (rawEvent.Mask & EventMask.Alt) != 0;


        string? hotkey = null;
        if (isCtrlPressed)
            hotkey += "Ctrl+";
        if (isShiftPressed)
            hotkey += "Shift+";
        if (isAltPressed)
            hotkey += "Alt+";

        hotkey += e.Data.KeyCode.ToString().Replace("Vc", "");
        return hotkey;
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _hook.Dispose();
    }
}