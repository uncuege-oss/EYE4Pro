using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class OrtaGerilimDialog : Window
{
    private readonly TekHatSemasiViewModel _vm = null!;
    private readonly Pano _anaPano = null!;
    private readonly bool _duzenle;

    // Çalışma kopyası — Kaydet'e kadar projeye dokunulmaz (İptal güvenli).
    private OrtaGerilimSistemi _og = new();
    private readonly List<OgHucre> _hucreler = new();
    private bool _hazir;
    private const string TrafoKatVarsayilan = "(en üstte / OG girişi)";

    public bool Silindi { get; private set; }

    public OrtaGerilimDialog() { InitializeComponent(); }

    public OrtaGerilimDialog(TekHatSemasiViewModel vm, Pano anaPano)
    {
        InitializeComponent();
        _vm = vm;
        _anaPano = anaPano;
        _duzenle = vm.AktifOrtaGerilim != null;

        BaslikText.Text = _duzenle ? "🔌 Orta Gerilim Sistemini Düzenle" : "🔌 Orta Gerilim Sistemi Ekle";
        AnaPanoText.Text = $"{anaPano.Ad}  ({anaPano.Kat})  — {anaPano.ToplamGorunurVA / 1000.0:N1} kVA";
        SilButton.IsVisible = _duzenle;
        KaydetButton.Content = _duzenle ? "Güncelle" : "Ekle";

        // Köşk tipi
        KoskTipiCombo.ItemsSource = new[] { "Bina Altı", "Dış Mekan Beton Köşk", "Harici Yapı" };

        // Trafo combo
        TrafoCombo.ItemsSource = new object[] { "(otomatik tayin)" }
            .Concat(vm.TrafoTipleri.Cast<object>()).ToList();
        TrafoCombo.SelectedIndex = 0;

        // Trafo katı combosu: ilk öğe "(en üstte / OG girişi)" = varsayılan; sonra binanın katları
        var trafoKatlari = new List<string> { TrafoKatVarsayilan };
        trafoKatlari.AddRange(vm.Proje.Bina?.KatEtiketleri() ?? new List<string>());
        TrafoKatCombo.ItemsSource = trafoKatlari;
        TrafoKatCombo.SelectedIndex = 0;

        // Hücre ekleme combosu
        HucreEkleCombo.ItemsSource = vm.OgHucreTipleri;
        if (vm.OgHucreTipleri.Count > 0) HucreEkleCombo.SelectedIndex = 0;

        // Bağlantı tipi
        BaglantiTipiCombo.ItemsSource = new[] { "Kablo", "Bar-24 Busbar Kanal", "Hücre (AG giriş)" };
        SebekeHucreMalzemeCombo.ItemsSource = new[] { "Alüminyum", "Bakır" };
        HucreTrafoMalzemeCombo.ItemsSource = new[] { "Alüminyum", "Bakır" };

        // Çalışma kopyasını hazırla
        if (_duzenle && vm.AktifOrtaGerilim != null)
            KopyalaIce(vm.AktifOrtaGerilim);
        else
            _og = new OrtaGerilimSistemi { AnaPanoAd = anaPano.Ad };

        // UI'yı çalışma kopyasından doldur
        KoskTipiCombo.SelectedIndex = (int)_og.KoskTipi;
        OgGerilimBox.Text = _og.OgGerilimKV.ToString("0.###");
        SebekeMvaBox.Text = _og.SebekeKisaDevreMVA.ToString("0.##");
        SebekeIkBox.Text = _og.SebekeIkKA.ToString("0.##");
        SebekeHucreMalzemeCombo.SelectedIndex = (int)_og.SebekeHucreXlpeKablosu.IletkenMalzemesi;
        SebekeHucreKesitBox.Text = _og.SebekeHucreXlpeKablosu.KesitMm2.ToString("0.##");
        SebekeHucreMesafeBox.Text = _og.SebekeHucreXlpeKablosu.UzunlukM.ToString("0.##");
        HucreTrafoMalzemeCombo.SelectedIndex = (int)_og.HucreTrafoXlpeKablosu.IletkenMalzemesi;
        HucreTrafoKesitBox.Text = _og.HucreTrafoXlpeKablosu.KesitMm2.ToString("0.##");
        HucreTrafoMesafeBox.Text = _og.HucreTrafoXlpeKablosu.UzunlukM.ToString("0.##");
        EmniyetBox.Text = _og.EmniyetFaktoru.ToString("0.##");
        if (!string.IsNullOrEmpty(_og.TrafoTanimAd))
        {
            var t = vm.TrafoTipleri.FirstOrDefault(x => x.Ad == _og.TrafoTanimAd);
            if (t != null) TrafoCombo.SelectedItem = t;
        }
        bool trafoKatGecerli = !string.IsNullOrWhiteSpace(_og.TrafoKat)
            && (vm.Proje.Bina?.KatEtiketleri()?.Contains(_og.TrafoKat) ?? false);
        TrafoKatCombo.SelectedItem = trafoKatGecerli ? _og.TrafoKat : TrafoKatVarsayilan;
        foreach (var h in _og.Hucreler) _hucreler.Add(h);
        BaglantiTipiCombo.SelectedIndex = (int)_og.CikisBaglanti.Tip;
        BaglantiUzunlukBox.Text = _og.CikisBaglanti.UzunlukM.ToString("0.##");
        ParalelKolBox.Text = _og.CikisBaglanti.ParalelKolSayisi.ToString();
        ReferansCombosuDoldur((int)_og.CikisBaglanti.Tip, _og.CikisBaglanti.ReferansAd);

        HucreListesiniTazele();
        _hazir = true;

        // Değişikliklerde özeti yenile
        KoskTipiCombo.SelectionChanged += (_, _) => OzetGuncelle();
        TrafoCombo.SelectionChanged += (_, _) => OzetGuncelle();
        BaglantiTipiCombo.SelectionChanged += (_, _) =>
        {
            ReferansCombosuDoldur(BaglantiTipiCombo.SelectedIndex, null);
            OzetGuncelle();
        };
        BaglantiReferansCombo.SelectionChanged += (_, _) => OzetGuncelle();
        OgGerilimBox.TextChanged += (_, _) => OzetGuncelle();
        SebekeMvaBox.TextChanged += (_, _) => OzetGuncelle();
        SebekeIkBox.TextChanged += (_, _) => OzetGuncelle();
        SebekeHucreMalzemeCombo.SelectionChanged += (_, _) => OzetGuncelle();
        SebekeHucreKesitBox.TextChanged += (_, _) => OzetGuncelle();
        SebekeHucreMesafeBox.TextChanged += (_, _) => OzetGuncelle();
        HucreTrafoMalzemeCombo.SelectionChanged += (_, _) => OzetGuncelle();
        HucreTrafoKesitBox.TextChanged += (_, _) => OzetGuncelle();
        HucreTrafoMesafeBox.TextChanged += (_, _) => OzetGuncelle();
        EmniyetBox.TextChanged += (_, _) => OzetGuncelle();
        BaglantiUzunlukBox.TextChanged += (_, _) => OzetGuncelle();
        ParalelKolBox.TextChanged += (_, _) => OzetGuncelle();

        OzetGuncelle();
    }

    private void KopyalaIce(OrtaGerilimSistemi kaynak)
    {
        _og = new OrtaGerilimSistemi
        {
            Id = kaynak.Id,
            AnaPanoAd = string.IsNullOrEmpty(kaynak.AnaPanoAd) ? _anaPano.Ad : kaynak.AnaPanoAd,
            KoskTipi = kaynak.KoskTipi,
            KoskGenislikMm = kaynak.KoskGenislikMm,
            KoskDerinlikMm = kaynak.KoskDerinlikMm,
            KoskYukseklikMm = kaynak.KoskYukseklikMm,
            OgGerilimKV = kaynak.OgGerilimKV,
            SebekeKisaDevreMVA = kaynak.SebekeKisaDevreMVA,
            SebekeIkKA = kaynak.SebekeIkKA,
            SebekeHucreXlpeKablosu = KabloKopyala(kaynak.SebekeHucreXlpeKablosu),
            HucreTrafoXlpeKablosu = KabloKopyala(kaynak.HucreTrafoXlpeKablosu),
            TrafoTanimAd = kaynak.TrafoTanimAd,
            TrafoKat = kaynak.TrafoKat,
            EmniyetFaktoru = kaynak.EmniyetFaktoru,
            Hucreler = kaynak.Hucreler.Select(h => new OgHucre
            {
                Id = h.Id, Ad = h.Ad, HucreTanimAd = h.HucreTanimAd, HucreTipi = h.HucreTipi
            }).ToList(),
            CikisBaglanti = new TrafoCikisBaglanti
            {
                Tip = kaynak.CikisBaglanti.Tip,
                ReferansAd = kaynak.CikisBaglanti.ReferansAd,
                UzunlukM = kaynak.CikisBaglanti.UzunlukM,
                ParalelKolSayisi = kaynak.CikisBaglanti.ParalelKolSayisi,
                HesaplananParalelKolSayisi = kaynak.CikisBaglanti.HesaplananParalelKolSayisi
            }
        };
    }

    private static OgXlpeKablo KabloKopyala(OgXlpeKablo? kaynak)
        => kaynak == null
            ? new OgXlpeKablo()
            : new OgXlpeKablo
            {
                IletkenMalzemesi = kaynak.IletkenMalzemesi,
                KesitMm2 = kaynak.KesitMm2,
                UzunlukM = kaynak.UzunlukM
            };

    private void ReferansCombosuDoldur(int tipIndex, string? seciliAd)
    {
        switch (tipIndex)
        {
            case 0: // Kablo
                BaglantiReferansCombo.IsEnabled = true;
                ParalelKolBox.IsEnabled = true;
                BaglantiReferansCombo.ItemsSource = new[] { "NYY", "N2XH", "NHXMH", "NYCWY", "N2XSY" };
                BaglantiReferansCombo.SelectedItem =
                    (object?)(seciliAd is not null && ((string[])BaglantiReferansCombo.ItemsSource).Contains(seciliAd) ? seciliAd : null)
                    ?? ((string[])BaglantiReferansCombo.ItemsSource).First();
                break;
            case 1: // Bar-24
                BaglantiReferansCombo.IsEnabled = true;
                ParalelKolBox.IsEnabled = false;
                BaglantiReferansCombo.ItemsSource = _vm.BusbarTipleri.ToList();
                BaglantiReferansCombo.SelectedItem =
                    _vm.BusbarTipleri.FirstOrDefault(b => b.Ad == seciliAd)
                    ?? _vm.BusbarTipleri.OrderByDescending(b => b.NominalAkimA).FirstOrDefault();
                break;
            default: // Hücre
                BaglantiReferansCombo.IsEnabled = false;
                ParalelKolBox.IsEnabled = false;
                BaglantiReferansCombo.ItemsSource = new[] { "AG Giriş Hücresi" };
                BaglantiReferansCombo.SelectedIndex = 0;
                break;
        }
    }

    private string ReferansAdiOku()
    {
        var sel = BaglantiReferansCombo.SelectedItem;
        return sel switch
        {
            BusbarTanim bt => bt.Ad,
            string s => s,
            _ => ""
        };
    }

    private void HucreListesiniTazele()
    {
        int sec = HucreListe.SelectedIndex;
        HucreListe.ItemsSource = _hucreler
            .Select((h, i) => $"{i + 1}. {OgHucreKisaAd(h.HucreTipi)} — {h.HucreTanimAd}")
            .ToList();
        if (sec >= 0 && sec < _hucreler.Count) HucreListe.SelectedIndex = sec;
    }

    private static string OgHucreKisaAd(OgHucreTipi t) => t switch
    {
        OgHucreTipi.Giris => "Giriş",
        OgHucreTipi.Olcu => "Ölçü",
        OgHucreTipi.BaraBaglama => "Bara Bağlama",
        OgHucreTipi.Cikis => "Çıkış",
        OgHucreTipi.TrafoKoruma => "Trafo Koruma",
        _ => t.ToString()
    };

    private void UiOku()
    {
        _og.AnaPanoAd = _anaPano.Ad;
        _og.KoskTipi = (KoskTipi)System.Math.Max(0, KoskTipiCombo.SelectedIndex);
        _og.OgGerilimKV = NumberHelper.Parse(OgGerilimBox.Text, 34.5);
        _og.SebekeKisaDevreMVA = NumberHelper.Parse(SebekeMvaBox.Text, 0);
        _og.SebekeIkKA = NumberHelper.Parse(SebekeIkBox.Text, 12.5);
        _og.SebekeHucreXlpeKablosu.IletkenMalzemesi =
            (OgKabloIletkenMalzemesi)System.Math.Max(0, SebekeHucreMalzemeCombo.SelectedIndex);
        _og.SebekeHucreXlpeKablosu.KesitMm2 = System.Math.Max(1, NumberHelper.Parse(SebekeHucreKesitBox.Text, 50));
        _og.SebekeHucreXlpeKablosu.UzunlukM = System.Math.Max(0, NumberHelper.Parse(SebekeHucreMesafeBox.Text, 10));
        _og.HucreTrafoXlpeKablosu.IletkenMalzemesi =
            (OgKabloIletkenMalzemesi)System.Math.Max(0, HucreTrafoMalzemeCombo.SelectedIndex);
        _og.HucreTrafoXlpeKablosu.KesitMm2 = System.Math.Max(1, NumberHelper.Parse(HucreTrafoKesitBox.Text, 50));
        _og.HucreTrafoXlpeKablosu.UzunlukM = System.Math.Max(0, NumberHelper.Parse(HucreTrafoMesafeBox.Text, 10));
        _og.EmniyetFaktoru = NumberHelper.Parse(EmniyetBox.Text, 1.0);
        if (_og.EmniyetFaktoru <= 0) _og.EmniyetFaktoru = 1.0;
        _og.TrafoTanimAd = (TrafoCombo.SelectedItem as TrafoTanim)?.Ad ?? "";
        string trafoKat = TrafoKatCombo.SelectedItem as string ?? TrafoKatVarsayilan;
        _og.TrafoKat = trafoKat == TrafoKatVarsayilan ? "" : trafoKat;
        _og.Hucreler = _hucreler.ToList();
        _og.CikisBaglanti.Tip = (TrafoCikisBaglantiTipi)System.Math.Max(0, BaglantiTipiCombo.SelectedIndex);
        _og.CikisBaglanti.ReferansAd = ReferansAdiOku();
        _og.CikisBaglanti.UzunlukM = NumberHelper.Parse(BaglantiUzunlukBox.Text, 10);
        _og.CikisBaglanti.ParalelKolSayisi = (int)System.Math.Max(1, NumberHelper.Parse(ParalelKolBox.Text, 1));
    }

    private void OzetGuncelle()
    {
        if (!_hazir) return;
        UiOku();
        OrtaGerilimHesapServisi.Hesapla(_og, _anaPano, _vm.Proje, _vm.Kutuphane);

        var trafo = _vm.TrafoTipleri.FirstOrDefault(t => t.Ad == _og.TrafoTanimAd);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Gerekli güç: {_og.GerekliKVAMin:N0} kVA  (yük {_og.HesaplananYukKVA:N1} kVA × {_og.EmniyetFaktoru:0.##})");
        if (trafo != null)
            sb.AppendLine($"Trafo: {trafo.Ad}  {(_og.TrafoYeterliMi ? "✓ yeterli" : "✗ YETERSİZ")}");
        else
            sb.AppendLine("Trafo: kütüphanede uygun trafo bulunamadı");
        sb.AppendLine($"Trafo akımı: AG {_og.TrafoAgAkimA:N0} A  •  OG {_og.TrafoOgAkimA:N1} A");
        sb.AppendLine($"AG bara Ik3: {_og.AgBaraIkKA:N1} kA");
        sb.AppendLine($"Şebeke → Hücre: {OrtaGerilimHesapServisi.XlpeKabloEtiketi(_og.SebekeHucreXlpeKablosu)}");
        sb.AppendLine($"Hücre → Trafo: {OrtaGerilimHesapServisi.XlpeKabloEtiketi(_og.HucreTrafoXlpeKablosu)}");
        var b = _og.CikisBaglanti;
        string baglanti = b.Tip switch
        {
            TrafoCikisBaglantiTipi.Kablo => $"Kablo {b.KesitMm2}",
            TrafoCikisBaglantiTipi.Bar24 => $"Bar-24 {b.ReferansAd}",
            _ => "AG Hücre"
        };
        if (b.Tip != TrafoCikisBaglantiTipi.Hucre)
            sb.AppendLine($"Bağlantı: {baglanti}  •  ΔU %{b.GerilimDususYuzde:0.##}");
        else
            sb.AppendLine($"Bağlantı: {baglanti}");

        var uyari = _og.Hucreler.Where(h => !h.AnmaAkimYeterliMi).ToList();
        if (uyari.Count > 0)
            sb.AppendLine($"⚠ {uyari.Count} hücrenin anma akımı yetersiz (OG akımı {_og.TrafoOgAkimA:N0} A).");

        OnerilenTrafoText.Text = trafo == null ? "" : $"Seçilen/önerilen: {trafo}";
        OzetText.Text = sb.ToString().TrimEnd();
    }

    private void HucreEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HucreEkleCombo.SelectedItem is not OgHucreTanim tanim) return;
        _hucreler.Add(new OgHucre { Ad = tanim.Ad, HucreTanimAd = tanim.Ad, HucreTipi = tanim.HucreTipi });
        HucreListesiniTazele();
        HucreListe.SelectedIndex = _hucreler.Count - 1;
        OzetGuncelle();
    }

    private void HucreSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = HucreListe.SelectedIndex;
        if (i < 0 || i >= _hucreler.Count) return;
        _hucreler.RemoveAt(i);
        HucreListesiniTazele();
        OzetGuncelle();
    }

    private void HucreYukari_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => HucreTasi(-1);
    private void HucreAsagi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => HucreTasi(1);

    private void HucreTasi(int yon)
    {
        int i = HucreListe.SelectedIndex;
        int j = i + yon;
        if (i < 0 || j < 0 || j >= _hucreler.Count) return;
        (_hucreler[i], _hucreler[j]) = (_hucreler[j], _hucreler[i]);
        HucreListesiniTazele();
        HucreListe.SelectedIndex = j;
        OzetGuncelle();
    }

    private async void KoskYerlesim_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UiOku();
        var pencere = new KoskYerlesimWindow(_og, _vm.Kutuphane);
        await pencere.ShowDialog(this);
    }

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UiOku();
        _vm.OrtaGerilimUygula(_og);
        Close(true);
    }

    private void Sil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.OrtaGerilimSil();
        Silindi = true;
        Close(true);
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
