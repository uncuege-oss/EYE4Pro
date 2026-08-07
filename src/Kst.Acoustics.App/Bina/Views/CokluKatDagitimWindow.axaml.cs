using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Bina.Services;

namespace Kst.Acoustics.App.Bina.Views;

/// Bir dağıtım satırı: aday küme + kullanıcının düzenleyebileceği ad/sıra/yükseklik/dahil.
public sealed class KatDagitimSatiri : INotifyPropertyChanged
{
    public DxfKatYukleyici.CokluKatAdayi Aday { get; }

    public KatDagitimSatiri(DxfKatYukleyici.CokluKatAdayi aday)
    {
        Aday = aday;
        _dahil = aday.OneriliDahil;
        _ad = aday.OnerilenAd;
        _sira = aday.SiraDegeri;
    }

    private bool _dahil;
    public bool Dahil
    {
        get => _dahil;
        set { if (_dahil == value) return; _dahil = value; OnPropertyChanged(); }
    }

    private string _ad;
    public string Ad
    {
        get => _ad;
        set { if (_ad == value) return; _ad = value; OnPropertyChanged(); }
    }

    private double _sira;
    public double Sira
    {
        get => _sira;
        set { if (_sira == value) return; _sira = value; OnPropertyChanged(); }
    }

    private double _yukseklikM = 3.0;
    public double YukseklikM
    {
        get => _yukseklikM;
        set { if (_yukseklikM == value) return; _yukseklikM = value; OnPropertyChanged(); }
    }

    public string BboxOzet => $"{Aday.Kume.GenislikM:0.#} × {Aday.Kume.DerinlikM:0.#} m  ·  {Aday.Kume.BboxAlanM2:0} m²";
    public string EtiketDurumu => Aday.EtiketBulundu ? "" : "⚠ Etiket yok";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// M50 §9.4 — çoklu kat DXF tespit edildiğinde açılan dağıtım diyaloğu.
/// Kullanıcı her kümenin ad/sıra/yükseklik/dahil bilgisini düzenler; onaylayınca
/// pencereyi açan taraf `OnaylananSatirlar`'ı sıraya göre (en yüksek kat ilk —
/// M50 liste konvansiyonu) okuyup `DxfKatYukleyici.CokluKatUret` ile katları üretir.
public partial class CokluKatDagitimWindow : Window
{
    public List<KatDagitimSatiri> Satirlar { get; }

    // Parameterless ctor: Avalonia AXAML loader için zorunlu.
    public CokluKatDagitimWindow() : this(new DxfKatYukleyici.CokluKatTespitSonucu(new List<DxfKatYukleyici.CokluKatAdayi>(), null)) { }

    public CokluKatDagitimWindow(DxfKatYukleyici.CokluKatTespitSonucu tespit)
    {
        InitializeComponent();

        Satirlar = tespit.Adaylar
            .OrderByDescending(a => a.SiraDegeri)
            .Select(a => new KatDagitimSatiri(a))
            .ToList();
        if (SatirList is { } lst) lst.ItemsSource = Satirlar;

        int dahilSayi = Satirlar.Count(s => s.Dahil);
        if (AciklamaText is { } t)
        {
            t.Text = $"Bu DXF'te {Satirlar.Count} ayrı plan kümesi bulundu ({dahilSayi} tanesi plan olarak öneriliyor). " +
                     "Ad/sıra/yükseklik alanlarını düzenleyin, dahil olmayacak kümelerin işaretini kaldırın, " +
                     "sonra \"Katlara Dağıt\"a basın."
                     + (tespit.UnitWarning is { } w ? $"\n⚠ {w}" : "");
        }
    }

    /// Onaylanan satırlar, sıraya göre (en yüksek kat İLK) — M50 liste konvansiyonuyla
    /// (index0 = en üst kat) doğrudan `Geo.Katlar`'a bu sırayla eklenebilir.
    public List<KatDagitimSatiri> OnaylananSatirlar =>
        Satirlar.Where(s => s.Dahil).OrderByDescending(s => s.Sira).ToList();

    private void Iptal_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void Onayla_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!Satirlar.Any(s => s.Dahil))
        {
            if (AciklamaText is { } t)
                t.Text = "⚠ En az bir kat seçilmeli — bir kümenin işaretini işaretleyin.";
            return;
        }
        Close(true);
    }
}
