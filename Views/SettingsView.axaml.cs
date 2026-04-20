// SettingsView.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorPickerApp.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace ColorPickerApp.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}