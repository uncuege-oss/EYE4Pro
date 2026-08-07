using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class JeneratorEkleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm = null!;
    private readonly Pano? _anaPano;
    private readonly ObservableCollection<JeneratorSatiri> _jeneratorSatirlari = new();
    private bool _yukleniyor;
    private const string KatVarsayilan = "(hedef pano katı)";

    public bool Silindi { get; private set; }

    public JeneratorEkleDialog() { InitializeComponent(); }

    public JeneratorEkleDialog(TekHatSemasiViewModel vm, Pano baslangicHedef)
    {
        InitializeComponent();
        _vm = vm;
        _anaPano = vm.Proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano);

        JeneratorListBox.ItemsSource = _jeneratorSatirlari;

        // ComboBox'ları doldur — "(otomatik)" ilk öğe
        JeneratorCombo.ItemsSource = new object[] { "(otomatik tayin)" }
            .Concat(vm.JeneratorTipleri.Cast<object>()).ToList();
        JeneratorCombo.SelectedIndex = 0;

        TransferSalterCombo.ItemsSource = new object[] { "(otomatik tayin)" }
            .Concat(vm.TransferSalterTipleri.Cast<object>()).ToList();
        TransferSalterCombo.SelectedIndex = 0;

        // Kat seçimi: ilk öğe "(hedef pano katı)" = varsayılan; sonra binanın kat etiketleri
        var katSecenekleri = new List<string> { KatVarsayilan };
        katSecenekleri.AddRange(vm.Proje.Bina?.KatEtiketleri() ?? new List<string>());
        KatCombo.ItemsSource = katSecenekleri;
        KatCombo.SelectedIndex = 0;

        // Lokal bölge için uygun hedef panolar (ana pano ve transfer panoları hariç)
        var lokalAdaylar = vm.Proje.TumPanolar
            .Where(p => p.PanoTipi is PanoTipi.TaliPano or PanoTipi.AydinlatmaPanosu or PanoTipi.UpsPanosu)
            .Select(p => new HedefPanoSecim(p))
            .ToList();
        HedefPanoCombo.ItemsSource = lokalAdaylar;

        // Başlangıç kapsamı: hedef ana pano ise tesis geneli, değilse lokal bölge
        _yukleniyor = true;
        bool baslangicLokal = _anaPano == null || baslangicHedef.PanoTipi != PanoTipi.AnaPano;
        KapsamTesis.IsEnabled = _anaPano != null;
        KapsamTesis.IsChecked = !baslangicLokal;
        KapsamLokal.IsChecked = baslangicLokal;
        if (baslangicLokal)
            HedefPanoCombo.SelectedItem = lokalAdaylar.FirstOrDefault(x => x.Pano.Ad == baslangicHedef.Ad)
                                          ?? lokalAdaylar.FirstOrDefault();
        _yukleniyor = false;

        HedefiGuncelle();

        KapsamTesis.IsCheckedChanged += (_, _) => { if (!_yukleniyor) HedefiGuncelle(); };
        KapsamLokal.IsCheckedChanged += (_, _) => { if (!_yukleniyor) HedefiGuncelle(); };
        HedefPanoCombo.SelectionChanged += (_, _) => { if (!_yukleniyor) HedefiGuncelle(); };
        ModPrime.IsCheckedChanged += (_, _) => { if (!_yukleniyor) OnerileriGuncelle(); };
        ModStandby.IsCheckedChanged += (_, _) => { if (!_yukleniyor) OnerileriGuncelle(); };
        EmniyetFaktoruBox.TextChanged += (_, _) => { if (!_yukleniyor) OnerileriGuncelle(); };
    }

    /// <summary>Seçili kapsam/combo'ya göre geçerli hedef panoyu döndürür.</summary>
    private Pano? GecerliHedef()
        => KapsamTesis.IsChecked == true
            ? _anaPano
            : (HedefPanoCombo.SelectedItem as HedefPanoSecim)?.Pano;

    /// <summary>
    /// Hedef pano değiştiğinde formu o hedefin mevcut grubuyla (varsa) yükler, yük/öneri metinlerini
    /// ve buton durumlarını günceller.
    /// </summary>
    private void HedefiGuncelle()
    {
        _yukleniyor = true;
        bool lokal = KapsamLokal.IsChecked == true;
        HedefPanoCombo.IsEnabled = lokal;
        HedefPanoLabel.IsEnabled = lokal;

        var hedef = GecerliHedef();
        var grup = hedef != null ? _vm.JeneratorGrubuBul(hedef) : null;
        bool duzenle = grup != null;

        BaslikText.Text = duzenle ? "⚡ Jeneratör / Senkron Panosu Düzenle" : "⚡ Jeneratör / Senkron Panosu Ekle";
        SilButton.IsVisible = duzenle;
        KaydetButton.Content = duzenle ? "Güncelle" : "Ekle";

        if (hedef == null)
        {
            AnaPanoText.Text = "Lokal bölge için bir tali pano seçin.";
            HesapYukText.Text = "—";
            _jeneratorSatirlari.Clear();
            _yukleniyor = false;
            OnerileriGuncelle();
            return;
        }

        string kapsamEtiket = lokal ? "Lokal bölge" : "Tesis geneli";
        AnaPanoText.Text = $"{hedef.Ad}  ({hedef.Kat})  — {kapsamEtiket}  •  Toplam: " +
                           $"{hedef.ToplamGucW / 1000.0:N1} kW / {hedef.ToplamGorunurVA / 1000.0:N1} kVA";
        HesapYukText.Text = $"{hedef.ToplamGucW / 1000.0:N1} kW  |  {hedef.GirisAkimA:N1} A";

        // Formu hedefin grubundan doldur (yoksa varsayılanlar)
        _jeneratorSatirlari.Clear();
        if (grup != null)
        {
            ModPrime.IsChecked = grup.Mod == JeneratorMod.Prime;
            ModStandby.IsChecked = grup.Mod == JeneratorMod.Standby;
            EmniyetFaktoruBox.Text = grup.EmniyetFaktoru.ToString("0.##");
            foreach (var j in grup.Jeneratorler)
            {
                var t = _vm.JeneratorTipleri.FirstOrDefault(x => x.Ad == j.JeneratorTanimAd);
                if (t != null) _jeneratorSatirlari.Add(new JeneratorSatiri(t, j.SenkronPanoKabloMesafeM, j.DisMekandaMi));
            }
            SenkronHedefMesafeBox.Text = grup.SenkronHedefPanoKabloMesafeM.ToString("0.##");
            // Manuel seçim → o şalter; otomatik → "(otomatik tayin)" (motor yükle güncellesin).
            TransferSalterCombo.SelectedItem = grup.TransferSalterManuel
                ? _vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == grup.TransferSalterAd)
                  ?? TransferSalterCombo.Items.OfType<object>().FirstOrDefault()
                : TransferSalterCombo.Items.OfType<object>().FirstOrDefault();
            bool katGecerli = !string.IsNullOrWhiteSpace(grup.Kat)
                && (_vm.Proje.Bina?.KatEtiketleri()?.Contains(grup.Kat) ?? false);
            KatCombo.SelectedItem = katGecerli ? grup.Kat : KatVarsayilan;
        }
        else
        {
            ModStandby.IsChecked = true;
            ModPrime.IsChecked = false;
            EmniyetFaktoruBox.Text = "1.25";
            SenkronHedefMesafeBox.Text = "10";
            TransferSalterCombo.SelectedIndex = 0;
            KatCombo.SelectedItem = KatVarsayilan;
        }

        _yukleniyor = false;
        OnerileriGuncelle();
    }

    private void OnerileriGuncelle()
    {
        var hedef = GecerliHedef();
        if (hedef == null)
        {
            ToplamKapasiteText.Text = "";
            OnerilenJenText.Text = "";
            OnerilenSalterText.Text = "";
            return;
        }

        var mod = ModPrime.IsChecked == true ? JeneratorMod.Prime : JeneratorMod.Standby;
        double emniyet = NumberHelper.Parse(EmniyetFaktoruBox.Text, 1.25);
        double yukKW = hedef.ToplamGucW / 1000.0;
        double yukKVA = hedef.ToplamGorunurVA / 1000.0;

        double seciliKW = _jeneratorSatirlari.Sum(s => JeneratorHesapServisi.JeneratorGucKW(s.Tanim, mod));
        double seciliKVA = _jeneratorSatirlari.Sum(s => JeneratorHesapServisi.JeneratorGucKVA(s.Tanim, mod));
        double gerekliKW = yukKW * emniyet;
        double gerekliKVA = yukKVA * emniyet;
        ToplamKapasiteText.Text = _jeneratorSatirlari.Count == 0
            ? "Liste boş: kaydetmede otomatik tek ünite seçilir."
            : $"Toplam: {seciliKW:N0} kW / {seciliKVA:N0} kVA  |  Gerekli: {gerekliKW:N0} kW / {gerekliKVA:N0} kVA";

        double kalanKW = Math.Max(0, gerekliKW - seciliKW);
        double kalanKVA = Math.Max(0, gerekliKVA - seciliKVA);
        bool grupYeterli = _jeneratorSatirlari.Count > 0 &&
            seciliKW >= gerekliKW &&
            seciliKVA >= gerekliKVA;
        var onerJen = grupYeterli
            ? null
            : JeneratorHesapServisi.UygunJeneratorSec(
                kalanKW > 0 ? kalanKW / Math.Max(1.0, emniyet) : yukKW,
                kalanKVA > 0 ? kalanKVA / Math.Max(1.0, emniyet) : yukKVA,
                mod, emniyet, _vm.Kutuphane);
        OnerilenJenText.Text = grupYeterli
            ? "Seçili jeneratör grubu yük için yeterli."
            : onerJen == null
                ? "Önerilen: kütüphanede uygun jeneratör yok"
                : $"Önerilen ek ünite: {onerJen.Ad}  (PRP {onerJen.PrimeGucKW}kW / ESP {onerJen.StandbyGucKW}kW)";

        if (_jeneratorSatirlari.Count > 0 || onerJen != null)
        {
            double jenAkim = _jeneratorSatirlari.Count > 0
                ? _jeneratorSatirlari.Sum(s => JeneratorHesapServisi.JeneratorAnmaAkimA(s.Tanim, mod))
                : JeneratorHesapServisi.JeneratorAnmaAkimA(onerJen!, mod);
            var onerSalter = JeneratorHesapServisi.UygunTransferSalterSec(
                hedef.GirisAkimA, jenAkim, _vm.Kutuphane);
            OnerilenSalterText.Text = onerSalter == null
                ? "Önerilen senkron/transfer şalteri: yok"
                : $"Önerilen senkron/transfer şalteri: {onerSalter.Ad}  ({onerSalter.AnmaAkimA}A {onerSalter.Tipi})";
        }
        else
        {
            OnerilenSalterText.Text = "";
        }
    }

    private void ListeyeEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var hedef = GecerliHedef();
        if (hedef == null) return;

        var tanim = JeneratorCombo.SelectedItem as JeneratorTanim;
        if (tanim == null)
        {
            var mod = ModPrime.IsChecked == true ? JeneratorMod.Prime : JeneratorMod.Standby;
            double emniyet = NumberHelper.Parse(EmniyetFaktoruBox.Text, 1.25);
            double yukKW = hedef.ToplamGucW / 1000.0;
            double yukKVA = hedef.ToplamGorunurVA / 1000.0;
            double seciliKW = _jeneratorSatirlari.Sum(s => JeneratorHesapServisi.JeneratorGucKW(s.Tanim, mod));
            double seciliKVA = _jeneratorSatirlari.Sum(s => JeneratorHesapServisi.JeneratorGucKVA(s.Tanim, mod));
            double kalanKW = Math.Max(0, yukKW * emniyet - seciliKW);
            double kalanKVA = Math.Max(0, yukKVA * emniyet - seciliKVA);
            if (_jeneratorSatirlari.Count > 0 && kalanKW <= 0 && kalanKVA <= 0)
                return;

            tanim = JeneratorHesapServisi.UygunJeneratorSec(
                kalanKW > 0 ? kalanKW / Math.Max(1.0, emniyet) : yukKW,
                kalanKVA > 0 ? kalanKVA / Math.Max(1.0, emniyet) : yukKVA,
                mod,
                emniyet,
                _vm.Kutuphane);
        }

        if (tanim != null)
            _jeneratorSatirlari.Add(new JeneratorSatiri(tanim, 10));
        OnerileriGuncelle();
    }

    private void ListedenSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (JeneratorListBox.SelectedItem is JeneratorSatiri satir)
            _jeneratorSatirlari.Remove(satir);
        OnerileriGuncelle();
    }

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var hedef = GecerliHedef();
        if (hedef == null)
        {
            AnaPanoText.Text = "⚠ Lokal bölge için bir tali pano seçin.";
            return;
        }

        var mod = ModPrime.IsChecked == true ? JeneratorMod.Prime : JeneratorMod.Standby;
        double emniyet = NumberHelper.Parse(EmniyetFaktoruBox.Text, 1.25);
        if (emniyet < 1.0) emniyet = 1.0;
        if (emniyet > 2.0) emniyet = 2.0;

        var jenTanim = JeneratorCombo.SelectedItem as JeneratorTanim;
        var salterTanim = TransferSalterCombo.SelectedItem as TransferSalterTanim;

        var secimler = _jeneratorSatirlari.Select(s => s.Tanim).ToList();
        var jeneratorKabloMesafeleri = _jeneratorSatirlari.Select(s => s.KabloMesafeM).ToList();
        var jeneratorDisMekanSecimleri = _jeneratorSatirlari.Select(s => s.DisMekandaMi).ToList();
        if (secimler.Count == 0 && jenTanim != null)
        {
            secimler.Add(jenTanim);
            jeneratorKabloMesafeleri.Add(10);
            jeneratorDisMekanSecimleri.Add(false);
        }

        var kapsam = KapsamTesis.IsChecked == true
            ? JeneratorBeslemeKapsami.TesisGeneli
            : JeneratorBeslemeKapsami.LokalBolge;

        string kat = KatCombo.SelectedItem as string ?? KatVarsayilan;
        if (kat == KatVarsayilan) kat = ""; // "" → hedef pano katı
        double senkronHedefMesafeM = Math.Max(0, NumberHelper.Parse(SenkronHedefMesafeBox.Text, 10));

        _vm.JeneratorGrubuUygula(
            hedef, mod, emniyet, secimler, salterTanim, kapsam, kat,
            jeneratorKabloMesafeleri, senkronHedefMesafeM, jeneratorDisMekanSecimleri);
        Close(true);
    }

    private void Sil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var hedef = GecerliHedef();
        if (hedef != null)
            _vm.JeneratorGrubuSil(hedef);
        Silindi = true;
        Close(true);
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private sealed class JeneratorSatiri
    {
        public JeneratorTanim Tanim { get; }
        public string Ad => Tanim.Ad;
        public string Kapasite => $"PRP {Tanim.PrimeGucKVA:N0} / ESP {Tanim.StandbyGucKVA:N0} kVA";
        public string KabloMesafeText { get; set; }
        public bool DisMekandaMi { get; set; }
        public double KabloMesafeM => Math.Max(0, NumberHelper.Parse(KabloMesafeText, 10));

        public JeneratorSatiri(JeneratorTanim tanim, double kabloMesafeM, bool disMekandaMi = false)
        {
            Tanim = tanim;
            KabloMesafeText = Math.Max(0, kabloMesafeM).ToString("0.##");
            DisMekandaMi = disMekandaMi;
        }

        public override string ToString()
            => $"{Tanim.Ad}  |  PRP {Tanim.PrimeGucKVA:N0} kVA / ESP {Tanim.StandbyGucKVA:N0} kVA";
    }

    private sealed class HedefPanoSecim
    {
        public Pano Pano { get; }
        public HedefPanoSecim(Pano pano) { Pano = pano; }
        public override string ToString() => $"{Pano.Ad}  ({Pano.Kat}) — {Pano.PanoTipi}";
    }
}
