using System;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class PanoBusbarBaglaDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly Pano _pano;
    private readonly BusbarHatti? _sabitBusbar;
    private int _sonMesafeTipi;
    private bool _programliMesafeTipiDegisimi;

    public PanoBusbarBaglaDialog() { InitializeComponent(); _vm = null!; _pano = null!; }

    public PanoBusbarBaglaDialog(TekHatSemasiViewModel vm, Pano pano, BusbarHatti? sabitBusbar = null)
    {
        InitializeComponent();
        _vm = vm;
        _pano = pano;
        _sabitBusbar = sabitBusbar;
        BaslikText.Text = sabitBusbar == null
            ? $"Panoyu Busbara Bağla — {_pano.Ad}"
            : $"Busbar Çıkış Kutusu — {_pano.Ad}";

        SigortaCombo.ItemsSource = PanoHesapServisi.StandartSigortaA;
        int onerilen = _pano.CikisKutusuSigortaA > 0
            ? _pano.CikisKutusuSigortaA
            : (_pano.GirisSigortaA > 0 ? _pano.GirisSigortaA : 16);
        SigortaCombo.SelectedItem = StandartSigortaSec(onerilen);

        BusbarCombo.ItemsSource = _vm.Busbarlar;
        BusbarCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        BusbarCombo.SelectionChanged += (_, _) => BusbarSecimiGuncelle();
        MesafeTipiCombo.ItemsSource = new[] { "Ana/üst panodan itibaren", "Bir önceki kutudan itibaren" };
        MesafeTipiCombo.SelectedIndex = 0;
        MesafeTipiCombo.SelectionChanged += (_, _) => MesafeTipiGuncelle();
        if (_vm.Busbarlar.Count > 0)
        {
            var mevcut = _sabitBusbar ?? _vm.Busbarlar.FirstOrDefault(b => b.Id == _pano.BaglandigiBusbarId);
            BusbarCombo.SelectedItem = mevcut ?? _vm.Busbarlar[0];
            BusbarCombo.IsEnabled = _sabitBusbar == null;
        }

        BusbarSecimiGuncelle();
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void Bagla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var bb = BusbarCombo.SelectedItem as BusbarHatti;
        if (bb == null) return;
        if (!NumberHelper.TryParse(PozisyonBox.Text, out double girilenMesafe) || girilenMesafe < 0) girilenMesafe = 0;
        double pozisyon = MesafeTipiCombo.SelectedIndex == 1
            ? OncekiKutuPozisyonu(bb) + girilenMesafe
            : girilenMesafe;
        if (!NumberHelper.TryParse(KabloMesafeBox.Text, out double kabloM) || kabloM <= 0) kabloM = 2;
        int sigortaA = SigortaCombo.SelectedItem is int i ? i : 16;

        _vm.PanoyuBusbarBagla(_pano, bb, pozisyon, sigortaA, kabloM);
        Close();
    }

    private void BusbarSecimiGuncelle()
    {
        if (BusbarCombo.SelectedItem is not BusbarHatti bb) return;

        var mevcutKutu = bb.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == _pano.Ad);
        if (mevcutKutu != null)
        {
            PozisyonBox.Text = mevcutKutu.PozisyonM.ToString("0.##");
            _programliMesafeTipiDegisimi = true;
            MesafeTipiCombo.SelectedIndex = 0;
            _programliMesafeTipiDegisimi = false;
            _sonMesafeTipi = 0;
            KabloMesafeBox.Text = mevcutKutu.KabloMesafeM.ToString("0.##");
            if (mevcutKutu.SigortaA > 0)
                SigortaCombo.SelectedItem = StandartSigortaSec(mevcutKutu.SigortaA);
        }
        else
        {
            PozisyonBox.Text = "0";
            _programliMesafeTipiDegisimi = true;
            MesafeTipiCombo.SelectedIndex = 0;
            _programliMesafeTipiDegisimi = false;
            _sonMesafeTipi = 0;
            KabloMesafeBox.Text = _pano.KabloMesafeM > 0 ? _pano.KabloMesafeM.ToString("0.##") : "2";
        }
        MesafeTipiGuncelle();
    }

    private static int StandartSigortaSec(int amper)
    {
        var secim = PanoHesapServisi.StandartSigortaA.FirstOrDefault(s => s >= amper);
        return secim > 0 ? secim : PanoHesapServisi.StandartSigortaA[^1];
    }

    private void MesafeTipiGuncelle()
    {
        if (BusbarCombo.SelectedItem is not BusbarHatti bb) return;
        int yeniTip = MesafeTipiCombo.SelectedIndex == 1 ? 1 : 0;
        double oncekiPozisyon = OncekiKutuPozisyonu(bb);

        if (!_programliMesafeTipiDegisimi && NumberHelper.TryParse(PozisyonBox.Text, out double mevcutDeger))
        {
            double mutlakPozisyon = _sonMesafeTipi == 1 ? oncekiPozisyon + mevcutDeger : mevcutDeger;
            double gosterilecek = yeniTip == 1 ? Math.Max(0, mutlakPozisyon - oncekiPozisyon) : mutlakPozisyon;
            PozisyonBox.Text = gosterilecek.ToString("0.##");
        }

        _sonMesafeTipi = yeniTip;
        if (yeniTip == 1)
        {
            var onceki = OncekiKutu(bb);
            PozisyonLabel.Text = "Bir Önceki Kutuya Uzaklık (m)";
            PozisyonYardimText.Text = onceki == null
                ? "Önceki kutu yok; girilen değer ana/üst panodan itibaren kabul edilir."
                : $"Önceki kutu: {onceki.BagliPanoAd} ({onceki.PozisyonM:0.##} m). Kaydedilen pozisyon = önceki + girilen mesafe.";
        }
        else
        {
            PozisyonLabel.Text = "Ana/Üst Panodan Uzaklık (m)";
            PozisyonYardimText.Text = "Bu değer busbar başlangıcından çıkış kutusuna kadar olan mesafedir.";
        }
    }

    private double OncekiKutuPozisyonu(BusbarHatti bb) => OncekiKutu(bb)?.PozisyonM ?? 0;

    private BusbarCikisKutusu? OncekiKutu(BusbarHatti bb)
    {
        var mevcut = bb.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == _pano.Ad);
        var digerleri = bb.CikisKutulari
            .Where(k => k.BagliPanoAd != _pano.Ad)
            .OrderBy(k => k.PozisyonM)
            .ThenBy(k => k.BagliPanoAd)
            .ToList();

        if (digerleri.Count == 0) return null;
        if (mevcut == null)
            return digerleri[^1];

        return digerleri.LastOrDefault(k => k.PozisyonM < mevcut.PozisyonM)
            ?? digerleri.LastOrDefault();
    }
}
