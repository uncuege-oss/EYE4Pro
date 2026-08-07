using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class NoiseBarrierView : UserControl
{
    public NoiseBarrierView()
    {
        InitializeComponent();
    }

    private void BackToLauncher_OnClick(object? sender, RoutedEventArgs e)
    {
        var launcher = new LauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = launcher;
        }

        launcher.Show();
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
            window.Close();
        }
    }

    private void OpenLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        var libWin = new NoiseBarrierLibraryWindow { DataContext = DataContext };
        if (VisualRoot is Window parent)
        {
            libWin.ShowDialog(parent);
        }
        else
        {
            libWin.Show();
        }
    }

    private void Recalculate_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is NoiseBarrierViewModel vm)
                vm.UpdateSimulation();
        }, DispatcherPriority.Background);
    }
}
