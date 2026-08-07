using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Bina.Views;
using Kst.Acoustics.App.Salon.Views;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.App.BuildingAcoustics.Views;

namespace Kst.Acoustics.App.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    /// Çoklu ekran fix — yeni modül penceresi launcher ile aynı ekranda açılmalı.
    /// Launcher'ın bulunduğu ekranın çalışma alanına göre yeni pencereyi merkezler;
    /// ekran tespit edilemezse en azından launcher'ın Position'ını kopyalar.
    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            // Pencere boyutu Width/Height NaN olabilir (ilk açılışta); fallback 1200×800
            int w = (int)(double.IsNaN(win.Width)  || win.Width  < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800  : win.Height);
            w = System.Math.Min(w, area.Width);
            h = System.Math.Min(h, area.Height);
            // Boyutu açıkça belirtilmemiş (NaN) pencereler içerik boyutuyla ekrandan
            // taşabiliyor → sol panel ScrollViewer'ı ekran altına kayıp erişilemez oluyordu.
            // Çalışma alanına sığacak şekilde sınırla.
            if (double.IsNaN(win.Width)  || win.Width  < 100) win.Width  = w;
            if (double.IsNaN(win.Height) || win.Height < 100) win.Height = h;
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
        this.Close();
    }

    private void OpenRoomAcoustics_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MainWindow { DataContext = new MainWindowViewModel() });

    private void BackToEngineeringHome_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new EngineeringHomeWindow());

    private void OpenPanelDesign_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new PanelDesignWindow());

    private void OpenSoundInsulation_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new SoundInsulationWindow());

    private void OpenNoiseBarrier_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new NoiseBarrierWindow());

    private void OpenImpactSurface_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new ImpactSurfaceWindow());

    private void OpenRoomModes_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new RoomModesWindow());

    private void OpenMeasurement_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new MeasurementWindow());

    private void OpenBinaAkustigi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new BinaProjesiWindow());

    private void OpenBinaGeometrisi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new BinaGeometriWindow(() => new LauncherWindow()));

    private void OpenSalonTasarla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(new SalonTasarlaWindow(() => new LauncherWindow()));
}
