using Avalonia.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class SoundInsulationView : UserControl
{
    public SoundInsulationView()
    {
        InitializeComponent();
        DataContext = new SoundInsulationViewModel();
    }

    private void OpenLibrary_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var libraryWindow = new SoundInsulationLibraryWindow();
        libraryWindow.DataContext = this.DataContext;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            libraryWindow.ShowDialog(parentWindow);
        }
        else
        {
            libraryWindow.Show();
        }
    }

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var launcher = new LauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = launcher;
        }
        launcher.Show();
        
        // Find the parent window and close it
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
            window.Close();
        }
    }
}
