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
        if (!NumberHelper.TryParse(KabloMesafeBox.Text, out double kabloM) || kabloM <= 0) kabloM = 2;
        int sigortaA = SigortaCombo.SelectedItem is int i ? i : 16;

        try
        {
            _vm.PanoyuBusbarBaglaKatKonumuyla(_pano, bb, girilenMesafe, sigortaA, kabloM);
            Close();
        }
        catch (InvalidOperationException ex)
        {
            BaslikText.Text = $"⚠ {ex.Message}";
        }
    }

    private void BusbarSecimiGuncelle()
    {
        if (BusbarCombo.SelectedItem is not BusbarHatti bb) return;

        var mevcutKutu = bb.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == _pano.Ad);
        if (mevcutKutu != null)
        {
            PozisyonBox.Text = (mevcutKutu.KatPozisyonM ?? mevcutKutu.PozisyonM).ToString("0.##");
            KabloMesafeBox.Text = mevcutKutu.KabloMesafeM.ToString("0.##");
            if (mevcutKutu.SigortaA > 0)
                SigortaCombo.SelectedItem = StandartSigortaSec(mevcutKutu.SigortaA);
        }
        else
        {
            PozisyonBox.Text = "0";
            KabloMesafeBox.Text = _pano.KabloMesafeM > 0 ? _pano.KabloMesafeM.ToString("0.##") : "2";
        }
        double katUzunlugu = BusbarYerlesimServisi.KatUzunlugu(bb, _pano.Kat);
        PozisyonYardimText.Text = bb.KatBolumleri.Count == 0
            ? $"Kat: {_pano.Kat}. Kat yerleşimi henüz tanımlı değil; girilen değer geçici olarak busbar başlangıcından ölçülür."
            : $"Kat: {_pano.Kat} • Bu kattaki busbar: {katUzunlugu:0.##} m. Konum yalnız bu katın başlangıcından ölçülür.";
    }

    private static int StandartSigortaSec(int amper)
    {
        var secim = PanoHesapServisi.StandartSigortaA.FirstOrDefault(s => s >= amper);
        return secim > 0 ? secim : PanoHesapServisi.StandartSigortaA[^1];
    }

}
