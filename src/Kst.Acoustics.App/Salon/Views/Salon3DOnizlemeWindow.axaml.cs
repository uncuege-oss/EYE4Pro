using Avalonia.Controls;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

/// M51 3D önizleme — SalonGeometri'yi GeometryPreviewControl ile izometrik gösterir.
public partial class Salon3DOnizlemeWindow : Window
{
    // Avalonia AXAML loader için zorunlu.
    public Salon3DOnizlemeWindow() : this(null) { }

    public Salon3DOnizlemeWindow(SalonGeometri? salon)
    {
        InitializeComponent();
        if (salon is not null) Yukle(salon);
    }

    public void Yukle(SalonGeometri salon)
    {
        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);

        if (BaslikText is { } b)
        {
            string ad = string.IsNullOrWhiteSpace(salon.SalonAdi) ? "Salon" : salon.SalonAdi;
            b.Text = $"{ad} — {salon.CizimOgeleri.Count} öğe, {yuzeyler.Count} yüzey, yükseklik {salon.SalonYuksekligiM:F1} m";
        }

        if (Onizleme3D is { } o)
            o.Surfaces = yuzeyler;
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}
