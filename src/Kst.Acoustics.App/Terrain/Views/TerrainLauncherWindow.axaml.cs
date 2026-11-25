using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.App.Terrain.Views;

public partial class TerrainLauncherWindow : Window
{
    public TerrainLauncherWindow()
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

    private void OpenHafriyat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new HafriyatWindow(() => new TerrainLauncherWindow()));

    private void OpenTemelOturtma_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new TemelOturtmaWindow(() => new TerrainLauncherWindow()));
}
