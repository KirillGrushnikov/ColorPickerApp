using System;
using System.Threading;

namespace ColorPickerApp;

/// <summary>
/// Гарантирует один экземпляр приложения. Повторный запуск не стартует новую копию,
/// а сигналит уже работающей показать главное окно.
/// </summary>
internal static class SingleInstance
{
    // Имя мьютекса также указано в installer/create setup.iss (AppMutex)
    private const string BaseName = "ColorPickerPro-E1AAE4FC-2D5A-4ADE-B2E8-D1E3B4BAC5A5";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static RegisteredWaitHandle? _activateRegistration;

    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, BaseName + "-mutex", out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, BaseName + "-activate");
        }
        catch
        {
            // Если именованные объекты недоступны - просто работаем без защиты от второй копии.
        }

        return true;
    }

    public static void ActivateExisting()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(BaseName + "-activate");
            activateEvent.Set();
        }
        catch
        {
            // Первая копия ещё не успела создать событие или уже завершается.
        }
    }

    /// <summary>Вызывает <paramref name="onActivate"/> (из пула потоков) при каждом повторном запуске.</summary>
    public static void ListenForActivation(Action onActivate)
    {
        if (_activateEvent is null)
            return;

        _activateRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => onActivate(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Освободить «единственный экземпляр» - перед перезапуском, чтобы новая копия не закрылась сразу.</summary>
    public static void Release()
    {
        try
        {
            _activateRegistration?.Unregister(null);
            _activateRegistration = null;
            _activateEvent?.Dispose();
            _activateEvent = null;
        }
        catch
        {
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // Мьютекс принадлежит другому потоку - всё равно закроем дескриптор
        }

        try
        {
            _mutex?.Dispose();
            _mutex = null;
        }
        catch
        {
        }
    }
}
