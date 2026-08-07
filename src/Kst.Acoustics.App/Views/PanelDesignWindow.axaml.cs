using Avalonia.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class PanelDesignWindow : Window
{
    public PanelDesignWindow()
    {
        InitializeComponent();
        DataContext = new PanelDesignViewModel();
    }

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var launcher = new LauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = launcher;
        }
        launcher.Show();
        this.Close();
    }
}
