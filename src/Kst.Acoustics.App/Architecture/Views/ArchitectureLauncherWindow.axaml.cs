using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Architecture.IhtiyacProgrami.Views;
using Kst.Acoustics.App.Architecture.KonseptPlan.Views;
using Kst.Acoustics.App.Architecture.Materials.Views;
using Kst.Acoustics.App.Architecture.Model3D.Views;
using Kst.Acoustics.App.Architecture.Reports.Views;
using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.App.Architecture.Views;

public partial class ArchitectureLauncherWindow : Window
{
    public ArchitectureLauncherWindow()
    {
        InitializeComponent();
    }

    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            var w = (int)(double.IsNaN(win.Width) || win.Width < 100 ? 1200 : win.Width);
            var h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800 : win.Height);
            w = System.Math.Min(w, area.Width);
            h = System.Math.Min(h, area.Height);
            win.Position = new PixelPoint(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2);
        }
        else
        {
            win.Position = Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;

        win.Show();
        Close();
    }

    private void BackToEngineeringHome_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new EngineeringHomeWindow());

    private void OpenIhtiyacProgrami_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new IhtiyacProgramiWindow(() => new ArchitectureLauncherWindow()));

    private void OpenKonseptPlan_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new KonseptPlanWindow(() => new ArchitectureLauncherWindow()));

    private void OpenModel3D_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new Model3DWindow(() => new ArchitectureLauncherWindow()));

    private void OpenMalzemeAtama_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MalzemeAtamaWindow(() => new ArchitectureLauncherWindow()));

    private void OpenMimariCikti_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MimariCiktiWindow(() => new ArchitectureLauncherWindow()));
}
