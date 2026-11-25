using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.App.Landscape.Views;

public partial class LandscapeLauncherWindow : Window
{
    public LandscapeLauncherWindow()
    {
        InitializeComponent();
    }

    /// Modül geçişinde mevcut pencere yerleşimini korur.
    private void OpenWindowOnSameScreen(Window win)
    {
        Kst.Acoustics.App.Views.WindowNavigation.CopyPlacement(this, win);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }

    private void BackToEngineeringHome_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new EngineeringHomeWindow());

    private void OpenPark_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ParkTasarimWindow(() => new LandscapeLauncherWindow()));
}
