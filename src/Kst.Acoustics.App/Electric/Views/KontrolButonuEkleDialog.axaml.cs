using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class KontrolButonuEkleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly Pano _kontrolPanosu;
    private bool _yukleniyor;

    public KontrolButonuEkleDialog()
    {
        InitializeComponent();
        _vm = null!;
        _kontrolPanosu = null!;
    }

    public KontrolButonuEkleDialog(TekHatSemasiViewModel vm, Pano kontrolPanosu)
    {
        InitializeComponent();
        _vm = vm;
        _kontrolPanosu = kontrolPanosu;

        BaslikText.Text = $"Kontrol Butonu Ekle — {kontrolPanosu.Ad}";
        KatlariYukle();
    }

    private void KatlariYukle()
    {
        _yukleniyor = true;
        var katlar = (_vm.Proje.Bina?.KatEtiketleri() ?? _vm.Proje.TumPanolar.Select(p => p.Kat).Distinct().ToList())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (katlar.Count == 0) katlar.Add(_kontrolPanosu.Kat);

        KatCombo.ItemsSource = katlar;
        var idx = katlar.FindIndex(k => string.Equals(k, _kontrolPanosu.Kat, StringComparison.OrdinalIgnoreCase));
        KatCombo.SelectedIndex = idx >= 0 ? idx : 0;
        _yukleniyor = false;
        PanolariYukle();
    }

    private void PanolariYukle()
    {
        if (_yukleniyor) return;
        var kat = KatCombo.SelectedItem as string;
        var panolar = _vm.KontrolKaynakPanolari(kat, _kontrolPanosu)
            .Select(p => new PanoSecim(p))
            .ToList();

        PanoCombo.ItemsSource = panolar;
        PanoCombo.SelectedIndex = panolar.Count > 0 ? 0 : -1;
        LinyeleriYukle();
    }

    private void LinyeleriYukle()
    {
        if (_yukleniyor) return;
        var pano = (PanoCombo.SelectedItem as PanoSecim)?.Pano;
        var linyeler = pano == null
            ? new List<LinyeSecim>()
            : _vm.KontrolIcinUygunDarbeLinyeleri(pano, _kontrolPanosu)
                .Select(l => new LinyeSecim(pano, l))
                .ToList();

        LinyeCombo.ItemsSource = linyeler;
        LinyeCombo.SelectedIndex = linyeler.Count > 0 ? 0 : -1;
        HepsiniEkleButton.IsEnabled = linyeler.Count > 0;
        HepsiniEkleButton.Content = linyeler.Count > 0 ? $"Hepsini Ekle ({linyeler.Count})" : "Hepsini Ekle";
        BilgiGuncelle();
    }

    private void BilgiGuncelle()
    {
        var secim = LinyeCombo.SelectedItem as LinyeSecim;
        EkleButton.IsEnabled = secim != null;
        if (secim == null)
        {
            BilgiText.Text = "Seçilen katta atanabilir darbe kesicili sigorta bulunmuyor.";
            return;
        }

        BilgiText.Text = $"{secim.Pano.Ad} panosundaki {secim.Linye.LinyeNo} linyesi için " +
                         $"{secim.Linye.SigortaAmper}A darbe kesici tetik butonu eklenecek.";
    }

    private void Kat_OnChanged(object? sender, SelectionChangedEventArgs e) => PanolariYukle();

    private void Pano_OnChanged(object? sender, SelectionChangedEventArgs e) => LinyeleriYukle();

    private void Linye_OnChanged(object? sender, SelectionChangedEventArgs e) => BilgiGuncelle();

    private void Ekle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LinyeCombo.SelectedItem is not LinyeSecim secim) return;
        var atama = _vm.KontrolButonuEkle(_kontrolPanosu, secim.Pano, secim.Linye);
        if (atama == null)
        {
            BilgiText.Text = "Bu sigorta başka bir kontrol panosuna atanmış veya artık uygun değil.";
            PanolariYukle();
            return;
        }

        Close(true);
    }

    private void HepsiniEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (PanoCombo.SelectedItem is not PanoSecim secim) return;
        int eklenen = _vm.KontrolButonlarininHepsiniEkle(_kontrolPanosu, secim.Pano);
        if (eklenen <= 0)
        {
            BilgiText.Text = "Seçili panoda atanabilir darbe kesicili sigorta kalmadı.";
            LinyeleriYukle();
            return;
        }

        Close(true);
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private sealed class PanoSecim
    {
        public Pano Pano { get; }
        public PanoSecim(Pano pano) { Pano = pano; }
        public override string ToString() => $"{Pano.Ad}  ({Pano.Kat})";
    }

    private sealed class LinyeSecim
    {
        public Pano Pano { get; }
        public Linye Linye { get; }

        public LinyeSecim(Pano pano, Linye linye)
        {
            Pano = pano;
            Linye = linye;
        }

        public override string ToString()
        {
            var aciklama = string.IsNullOrWhiteSpace(Linye.Aciklama) ? "" : $" — {Linye.Aciklama}";
            return $"{Linye.LinyeNo}  {Linye.SigortaAmper}A{aciklama}";
        }
    }
}
