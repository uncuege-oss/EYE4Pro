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
        Kst.Acoustics.App.Views.WindowNavigation.CopyPlacement(this, win);

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
