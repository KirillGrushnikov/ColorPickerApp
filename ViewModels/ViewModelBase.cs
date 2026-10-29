using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorPickerApp.ViewModels;

/// <summary>
/// Минимальная реализация INotifyPropertyChanged вместо ReactiveObject:
/// не тянет ReactiveUI / Splat / System.Reactive / DynamicData при запуске.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected void RaisePropertyChanged(PropertyChangedEventArgs args) =>
        PropertyChanged?.Invoke(this, args);

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
