using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class PanoDuzenleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly Pano _pano;
    private bool _govdeUiYukleniyor;

    public PanoDuzenleDialog() { InitializeComponent(); _vm = null!; _pano = null!; }

    public PanoDuzenleDialog(TekHatSemasiViewModel vm, Pano pano)
    {
        InitializeComponent();
        _vm = vm;
        _pano = pano;
        _govdeUiYukleniyor = true;

        BaslikText.Text = $"Panoyu Düzenle — {pano.Ad}";
        AdBox.Text = pano.Ad;

        // Kat seçenekleri proje binasından (null güvenli)
        var katlar = vm?.Proje?.Bina?.KatEtiketleri() ?? new List<string> { "Z" };
        KatCombo.ItemsSource = katlar;
        var idx = katlar.IndexOf(pano.Kat ?? "Z");
        KatCombo.SelectedIndex = idx >= 0 ? idx : 0;

        TipCombo.SelectedIndex = pano.PanoTipi switch
        {
            PanoTipi.AnaPano => 1,
            PanoTipi.AydinlatmaPanosu => 2,
            PanoTipi.Kompanzasyon => 3,
            PanoTipi.KontrolPanosu => 4,
            PanoTipi.YanginPanosu => 5,
            _ => 0
        };
        SayacliCheck.IsChecked = pano.AydinlatmaSayacli;
        SayacliCheck.IsVisible = pano.PanoTipi == PanoTipi.AydinlatmaPanosu;

        PanoBoyutlandirmaServisi.Hesapla(pano);
        GovdeTipiCombo.SelectedIndex = pano.GovdeTipi == PanoGovdeTipi.DikiliTip ? 1 : 0;
        ModulAlaniText.Text = $"{pano.ModulSayisi} modül / {pano.ModulAlaniM2:N3} m²";
        KlemensAlaniText.Text = $"{pano.KlemensSayisi} adet / {pano.KlemensAlaniM2:N3} m²";
        GerekliAlanText.Text = $"{pano.GerekliPanoAlaniM2:N3} m²";

        PanoOlcuCombo.ItemsSource = PanoBoyutlandirmaServisi.StandartOlculer;
        PanoOlcuCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PanoOlcusuSecenegi.Etiket));
        PanoOlcuCombo.SelectedItem = PanoBoyutlandirmaServisi.OlcuyuBul(pano.PanoGenislikM, pano.PanoYukseklikM)
            ?? PanoBoyutlandirmaServisi.OtomatikOlcuyuSec(pano.GerekliPanoAlaniM2);
        OtomatikOlcuCheck.IsChecked = pano.PanoOlcusuOtomatik;

        DikiliDerinlikCombo.ItemsSource = PanoBoyutlandirmaServisi.DikiliTipDerinlikleriCm;
        DikiliDerinlikCombo.SelectedItem = PanoBoyutlandirmaServisi.DikiliTipDerinlikleriCm.Contains(pano.PanoDerinlikCm)
            ? pano.PanoDerinlikCm
            : 90;
        _govdeUiYukleniyor = false;
        GovdeAlanlariniGuncelle();

        // Eşzamanlılık faktörü yalnız alt panolar için anlamlı (üst panoya yansır)
        DiversiteBox.Text = pano.EszamanlilikFaktoru.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        DiversitePanel.IsVisible = pano.UstPano != null;

        // Harmonik filtre paneli (Kompanzasyon)
        HarmonikPanel.IsVisible = pano.PanoTipi == PanoTipi.Kompanzasyon;
        HarmonikFiltreliCheck.IsChecked = pano.HarmonikFiltreliMi;
        HarmonikCombo.ItemsSource = _vm.HarmonikFiltreTipleri;
        HarmonikCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (pano.SeciliHarmonikFiltre != null)
            HarmonikCombo.SelectedItem = pano.SeciliHarmonikFiltre;
        else if (_vm.HarmonikFiltreTipleri.Count > 0)
            HarmonikCombo.SelectedIndex = 0;
        HarmonikSecimPanel.IsVisible = pano.HarmonikFiltreliMi;

        // Kompanzasyon Bileşen Override Paneli
        KompOverridePanel.IsVisible = pano.PanoTipi == PanoTipi.Kompanzasyon;
        ElleAyarCheck.IsChecked = pano.KompanzasyonElleAyarlandi;

        KompRoleCombo.ItemsSource = _vm.KompanzasyonRolesiTipleri;
        KompRoleCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (pano.SeciliKompanzasyonRolesi != null) KompRoleCombo.SelectedItem = pano.SeciliKompanzasyonRolesi;
        else if (_vm.KompanzasyonRolesiTipleri.Count > 0) KompRoleCombo.SelectedIndex = 0;

        ReaktorCombo.ItemsSource = _vm.ReaktorTipleri;
        ReaktorCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (pano.SeciliReaktor != null) ReaktorCombo.SelectedItem = pano.SeciliReaktor;
        else if (_vm.ReaktorTipleri.Count > 0) ReaktorCombo.SelectedIndex = 0;

        SvcCombo.ItemsSource = _vm.SvcSurucuTipleri;
        SvcCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (pano.SeciliSvcSurucu != null) SvcCombo.SelectedItem = pano.SeciliSvcSurucu;
        else if (_vm.SvcSurucuTipleri.Count > 0) SvcCombo.SelectedIndex = 0;

        BicakliCombo.ItemsSource = _vm.BicakliSigortaTipleri;
        BicakliCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (pano.GirisBicakliSigorta != null) BicakliCombo.SelectedItem = pano.GirisBicakliSigorta;
        else if (_vm.BicakliSigortaTipleri.Count > 0) BicakliCombo.SelectedIndex = 0;
    }

    private void HarmonikFiltreli_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HarmonikSecimPanel != null)
            HarmonikSecimPanel.IsVisible = HarmonikFiltreliCheck.IsChecked ?? false;
    }

    private void Tip_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SayacliCheck != null)
            SayacliCheck.IsVisible = TipCombo.SelectedIndex == 2;
        bool isKomp = TipCombo.SelectedIndex == 3;
        if (HarmonikPanel != null) HarmonikPanel.IsVisible = isKomp;
        if (KompOverridePanel != null) KompOverridePanel.IsVisible = isKomp;
    }

    private void GovdeTipi_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_govdeUiYukleniyor)
            GovdeAlanlariniGuncelle();
    }

    private void OtomatikOlcu_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_govdeUiYukleniyor) return;
        if (OtomatikOlcuCheck.IsChecked == true)
            PanoOlcuCombo.SelectedItem = PanoBoyutlandirmaServisi.OtomatikOlcuyuSec(_pano.GerekliPanoAlaniM2);
        GovdeAlanlariniGuncelle();
    }

    private void GovdeAlanlariniGuncelle()
    {
        bool dikili = GovdeTipiCombo.SelectedIndex == 1;
        DikiliDerinlikPanel.IsVisible = dikili;
        PanoOlcuLabel.Text = dikili ? "Pano Ön Yüz Ölçüsü" : "Duvar Tipi Pano Ölçüsü";
        PanoOlcuCombo.IsEnabled = OtomatikOlcuCheck.IsChecked != true;
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Ad değişikliği: çocuk/busbar/jeneratör/UPS referanslarını da güncelle (kopmayı önler)
        if (!string.IsNullOrWhiteSpace(AdBox.Text))
            _vm.PanoyuYenidenAdlandir(_pano, AdBox.Text.Trim());
        if (KatCombo.SelectedItem is string k) _pano.Kat = k;
        _pano.PanoTipi = TipCombo.SelectedIndex switch
        {
            1 => PanoTipi.AnaPano,
            2 => PanoTipi.AydinlatmaPanosu,
            3 => PanoTipi.Kompanzasyon,
            4 => PanoTipi.KontrolPanosu,
            5 => PanoTipi.YanginPanosu,
            _ => PanoTipi.TaliPano
        };
        if (_pano.PanoTipi == PanoTipi.KontrolPanosu)
        {
            _pano.UstPano?.AltPanolar.Remove(_pano);
            _pano.UstPano = null;
            _pano.UstPanoAd = null;
            _pano.BaglantiTipi = BaglantiTipi.Kablo;
            _pano.BaglandigiBusbarId = null;
            _pano.CikisKutusuSigortaA = 0;
        }
        _pano.AydinlatmaSayacli = SayacliCheck.IsChecked ?? false;

        _pano.GovdeTipi = GovdeTipiCombo.SelectedIndex == 1
            ? PanoGovdeTipi.DikiliTip
            : PanoGovdeTipi.DuvarTipi;
        _pano.PanoOlcusuOtomatik = OtomatikOlcuCheck.IsChecked ?? true;
        if (PanoOlcuCombo.SelectedItem is PanoOlcusuSecenegi olcu)
        {
            _pano.PanoGenislikM = olcu.GenislikM;
            _pano.PanoYukseklikM = olcu.YukseklikM;
        }
        _pano.PanoDerinlikCm = DikiliDerinlikCombo.SelectedItem is int derinlik ? derinlik : 90;

        if (Kst.Acoustics.Core.Electric.Calculation.NumberHelper.TryParse(DiversiteBox.Text, out double ks))
            _pano.EszamanlilikFaktoru = ks <= 0 ? 1.0 : System.Math.Min(1.0, ks);
        _pano.HarmonikFiltreliMi = (HarmonikFiltreliCheck.IsChecked ?? false) && _pano.PanoTipi == PanoTipi.Kompanzasyon;
        _pano.SeciliHarmonikFiltre = _pano.HarmonikFiltreliMi
            ? (HarmonikCombo.SelectedItem as HarmonikFiltreTanim)
            : null;

        // Kompanzasyon bileşenleri (override)
        if (_pano.PanoTipi == PanoTipi.Kompanzasyon)
        {
            _pano.KompanzasyonElleAyarlandi = ElleAyarCheck.IsChecked ?? false;
            if (_pano.KompanzasyonElleAyarlandi)
            {
                _pano.SeciliKompanzasyonRolesi = KompRoleCombo.SelectedItem as KompanzasyonRolesiTanim;
                _pano.SeciliReaktor = ReaktorCombo.SelectedItem as ReaktorTanim;
                _pano.SeciliSvcSurucu = SvcCombo.SelectedItem as SvcSurucuTanim;
                _pano.GirisBicakliSigorta = BicakliCombo.SelectedItem as BicakliSigortaTanim;
            }
        }
        _vm.TumunuHesapla();
        Close();
    }
}
