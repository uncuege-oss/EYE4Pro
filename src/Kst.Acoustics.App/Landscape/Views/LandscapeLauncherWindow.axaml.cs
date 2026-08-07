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

    /// Çoklu ekran fix — yeni pencere bu pencerenin ekranında merkezlensin.
    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = (int)(double.IsNaN(win.Width) || win.Width < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800 : win.Height);
            w = System.Math.Min(w, area.Width);
            h = System.Math.Min(h, area.Height);
            win.Position = new PixelPoint(
                area.X + (area.Width - w) / 2,
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

    private void BackToEngineeringHome_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new EngineeringHomeWindow());

    private void OpenPark_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ParkTasarimWindow(() => new LandscapeLauncherWindow()));
}
