using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorPickerApp.Services;
using ColorPickerApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace ColorPickerApp.Views
{
    public partial class HotkeyDialogView : Window
    {

        public HotkeyDialogView()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            var hotkeyService = App.ServiceProvider.GetRequiredService<GlobalHotkeyService>();
            hotkeyService.Start();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            
            var hotkeyService = App.ServiceProvider.GetRequiredService<GlobalHotkeyService>();
            hotkeyService.Stop();

            HotkeyDialogViewModel ViewModel = DataContext as HotkeyDialogViewModel;
            this.KeyDown += OnKeyDown;
            Disposable.Create(() => this.KeyDown -= OnKeyDown);


            // При подтверждении закрываем окно с результатом
            ViewModel?.ConfirmCommand.Subscribe(hotkey =>
            {
                Close(hotkey);
            });

            // При отмене закрываем с null
            ViewModel?.CancelCommand.Subscribe(_ =>
            {
                Close(null);
            });
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            HotkeyDialogViewModel ViewModel = DataContext as HotkeyDialogViewModel;
            ViewModel?.UpdateHotkeyFromKeyDown(sender, e);
        }
    }
}