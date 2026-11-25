using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Bina.Views;
using Kst.Acoustics.App.Salon.Views;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.App.BuildingAcoustics.Views;
using Kst.Acoustics.App.Electric.BasitProjeCiz.Views;
using Kst.Acoustics.App.Electric.CevreAydinlatma;
using Kst.Acoustics.App.Electric.KesifHazirla;
using Kst.Acoustics.App.Electric.Views;
using Kst.Acoustics.App.Electric.ZayifAkimTekHat;
using Kst.Acoustics.App.Architecture.IhtiyacProgrami.Views;
using Kst.Acoustics.App.Architecture.KonseptPlan.Views;
using Kst.Acoustics.App.Architecture.Materials.Views;
using Kst.Acoustics.App.Architecture.Model3D.Views;
using Kst.Acoustics.App.Architecture.Reports.Views;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.App.Architecture.Views;
using Kst.Acoustics.App.Terrain.Views;
using Kst.Acoustics.App.Landscape.Views;

namespace Kst.Acoustics.App.Views;

public partial class EngineeringHomeWindow : Window
{
    public EngineeringHomeWindow()
    {
        InitializeComponent();
        UpdateResponsiveSizes();
        SizeChanged += (_, _) => UpdateResponsiveSizes();
    }

    private void UpdateResponsiveSizes()
    {
        // Layout fills the available area; scale typography and logos without
        // distorting their aspect ratio or making large monitors use tiny labels.
        var scale = System.Math.Clamp(System.Math.Min(
            ClientSize.Width / 1180.0, ClientSize.Height / 760.0), 0.78, 1.65);
        Resources["ModuleFontSize"] = 12.0 * scale;
        Resources["FamilyFontSize"] = 18.0 * scale;
        Resources["FamilyLogoWidth"] = 130.0 * scale;
        Resources["FamilyLogoHeight"] = 50.0 * scale;
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
    private void OpenSharedBuilding_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new Kst.Acoustics.App.Bina.Views.BinaGeometriWindow(() => new EngineeringHomeWindow()));

    private void OpenSharedHall_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new Kst.Acoustics.App.Salon.Views.SalonTasarlaWindow(() => new EngineeringHomeWindow()));
    private void AcousticsOpenRoomAcoustics_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MainWindow { DataContext = new MainWindowViewModel() });

    private void AcousticsOpenSoundInsulation_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new SoundInsulationWindow());

    private void AcousticsOpenPanelDesign_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new PanelDesignWindow());

    private void AcousticsOpenBinaAkustigi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new BinaProjesiWindow());

    private void AcousticsOpenImpactSurface_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ImpactSurfaceWindow());

    private void AcousticsOpenNoiseBarrier_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new NoiseBarrierWindow());

    private void AcousticsOpenRoomModes_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new RoomModesWindow());

    private void AcousticsOpenMeasurement_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MeasurementWindow());

    private void ElectricOpenTekHat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new TekHatSemasiWindow());

    private void ElectricOpenBasitProjeCiz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new BasitProjeCizWindow());

    private void ElectricOpenZayifAkimProjeCiz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new BasitProjeCizWindow(
            Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcDisiplin.ZayifAkim));

    private void ElectricOpenZayifAkimTekHat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ZayifAkimTekHatWindow());

    private void ElectricOpenKesifHazirla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new KesifHazirlaWindow());

    private void ElectricOpenCevreAydinlatma_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new CevreAydinlatmaWindow());

    private void ArchitectureOpenIhtiyacProgrami_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new IhtiyacProgramiWindow(() => new EngineeringHomeWindow()));

    private void ArchitectureOpenKonseptPlan_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new KonseptPlanWindow(() => new EngineeringHomeWindow()));

    private void ArchitectureOpenModel3D_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new Model3DWindow(() => new EngineeringHomeWindow()));

    private void ArchitectureOpenMalzemeAtama_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MalzemeAtamaWindow(() => new EngineeringHomeWindow()));

    private void ArchitectureOpenMimariCikti_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MimariCiktiWindow(() => new EngineeringHomeWindow()));

    private void TerrainOpenHafriyat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new HafriyatWindow(() => new EngineeringHomeWindow()));

    private void TerrainOpenTemelOturtma_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new TemelOturtmaWindow(() => new EngineeringHomeWindow()));

    private void LandscapeOpenPark_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ParkTasarimWindow(() => new EngineeringHomeWindow()));
}
