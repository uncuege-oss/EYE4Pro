using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.App.Bina.Views;

/// M50 3D önizleme — BinaGeometri'yi GeometryPreviewControl ile izometrik gösterir.
public partial class Bina3DOnizlemeWindow : Window
{
    // Avalonia AXAML loader için zorunlu.
    public Bina3DOnizlemeWindow() : this(null) { }

    public Bina3DOnizlemeWindow(BinaGeometri? geo)
    {
        InitializeComponent();
        if (geo is not null) Yukle(geo);
    }

    public void Yukle(BinaGeometri geo)
    {
        if (BaslikText is { } b)
        {
            string ad = string.IsNullOrWhiteSpace(geo.BinaAdi) ? "Bina" : geo.BinaAdi;
            int odaSayisi = geo.Katlar.Sum(k => k.Odalar.Count);
            int birlesim = geo.DuseyBirlesimleri.Count(x => x.AktifMi);
            b.Text = birlesim > 0
                ? $"{ad} — {geo.Katlar.Count} kat, {odaSayisi} oda, {birlesim} düşey birleşim"
                : $"{ad} — {geo.Katlar.Count} kat, {odaSayisi} oda";
        }

        if (Onizleme3D is not { } o) return;
        o.Surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);

        var bg = Bina3DGorunumServisi.ArkaPlanSegmentleri(geo);
        o.BackgroundSegments = bg.Count > 0 ? bg : null;

        // BinaAciklikTipi → BpcAciklikTipi (GeometryPreviewControl Bpc enum bekler)
        var ack = Bina3DGorunumServisi.Acikliklar3D(geo)
            .Select(a => (a.Bas, a.Bit, a.Iptal,
                a.Tip == BinaAciklikTipi.Pencere ? BpcAciklikTipi.Pencere : BpcAciklikTipi.Kapı))
            .ToList();
        o.Acikliklar = ack.Count > 0 ? ack : null;
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}
