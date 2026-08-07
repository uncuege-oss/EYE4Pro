using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Architecture.Views;
using Kst.Acoustics.App.Terrain.Views;
using Kst.Acoustics.App.Landscape.Views;

namespace Kst.Acoustics.App.Views;

public partial class EngineeringHomeWindow : Window
{
    public EngineeringHomeWindow()
    {
        InitializeComponent();
    }

    /// Çoklu ekran fix — yeni pencere bu pencerenin ekranında merkezlensin.
    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = (int)(double.IsNaN(win.Width)  || win.Width  < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800  : win.Height);
            w = System.Math.Min(w, area.Width);
            h = System.Math.Min(h, area.Height);
            win.Position = new PixelPoint(
                area.X + (area.Width  - w) / 2,
                area.Y + (area.Height - h) / 2);
        }
        else
        {
            win.Position = this.Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }

    private void OpenAcoustics_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new LauncherWindow());

    private void OpenElectric_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ElectricLauncherWindow());

    private void OpenMechanics_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MechanicsLauncherWindow());

    private void OpenArchitecture_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ArchitectureLauncherWindow());

    private void OpenTerrain_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new TerrainLauncherWindow());

    private void OpenLandscape_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new LandscapeLauncherWindow());
}
