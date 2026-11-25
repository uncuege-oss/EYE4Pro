using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class BasitProjeCizWindow
{
    private void GecisMahali_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: BasitProjeOda oda } secim)
            _vm.GeçisMahaliniAyarla(oda, secim.IsChecked == true);
    }

    private sealed record SigortaYeri(string Id, string Ad, int Oneri, BpcPanoSigortasi? Kayit)
    {
        public bool Yedek => Id.StartsWith(AydinlatmaTasarimServisi.YedekOnEk, StringComparison.Ordinal);
        public override string ToString() => Kayit switch
        {
            { Amper: > 0 } => $"{Ad}  ·  {Kayit.Amper} A{(Kayit.Manuel ? " (elle)" : "")}",
            { Manuel: true } => $"{Ad} (kaldırıldı)",
            _ => Ad
        };
    }

    private static List<SigortaYeri> SigortaSecenekleri(PanoOzet ozet, bool prizDahil)
    {
        var kayitlar = ozet.Pano.AydinlatmaSigortalari;
        var yerler = AydinlatmaTasarimServisi.SigortaYerleri(ozet, prizDahil)
            .Select(x => new SigortaYeri(x.Id, x.Ad, x.Amper, kayitlar?.GetValueOrDefault(x.Id))).ToList();
        if (kayitlar != null)
        {
            int sira = 0;
            foreach (var kv in kayitlar.Where(kv => kv.Key.StartsWith(AydinlatmaTasarimServisi.YedekOnEk, StringComparison.Ordinal)))
                yerler.Add(new(kv.Key, $"Yedek {++sira}", kv.Value.Amper, kv.Value));
        }
        yerler.Add(new(AydinlatmaTasarimServisi.YedekOnEk + Guid.NewGuid().ToString("N"), "Yeni yedek çıkış", 10, null));
        return yerler;
    }

    private async void PanoSigortalari_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SeciliKat is not { } kat) return;
        bool prizDahil = _vm.PrizAkisiAktif;
        var panolar = _vm.PrizSigortaHesaplari().Where(o => kat.Elemanlar.Contains(o.Pano)).ToList();
        if (panolar.Count == 0) { _vm.DurumMesaji = "Bu kata önce pano yerleştirin."; return; }
        var panoSec = new ComboBox { Name = "SigortaPano", ItemsSource = panolar.Select(o =>
            string.IsNullOrWhiteSpace(o.Pano.EkranAdi) || o.Pano.EkranAdi == o.Ad ? o.Ad : $"{o.Pano.EkranAdi} · {o.Ad}").ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var toplam = new TextBlock { Name = "SigortaYuk" };
        var cikisSec = new ComboBox { Name = "SigortaCikis", HorizontalAlignment = HorizontalAlignment.Stretch };
        var oneri = new TextBlock { Name = "SigortaOneri" };
        var amper = new ComboBox { Name = "SigortaAmper", ItemsSource = LinyeHesapServisi.StandartSigortaDegerleri, HorizontalAlignment = HorizontalAlignment.Stretch };
        var uygula = new Button { Content = "Kaydet" };
        var otomatik = new Button { Content = "Otomatiğe dön" };
        var sil = new Button { Content = "Sigortayı kaldır" };
        var kapat = new Button { Content = "Kapat" };
        var dialog = new Window
        {
            Title = "Pano giriş ve çıkış sigortaları", Width = 510, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16), Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Pano" }, panoSec, toplam,
                    new TextBlock { Text = "Sigorta yeri" }, cikisSec, oneri,
                    new TextBlock { Text = "Anma akımı (A)" }, amper,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { uygula, otomatik, sil, kapat } }
                }
            }
        };
        void CikislariYenile(string? seciliId)
        {
            if (panoSec.SelectedIndex < 0) return;
            var ozet = panolar[panoSec.SelectedIndex];
            toplam.Text = $"Aydınlatma + priz yükü   {ozet.ToplamGucW:N0} W";
            var yerler = SigortaSecenekleri(ozet, prizDahil);
            cikisSec.ItemsSource = yerler;
            cikisSec.SelectedItem = yerler.FirstOrDefault(y => y.Id == seciliId) ?? yerler[0];
        }
        panoSec.SelectionChanged += (_, _) => CikislariYenile(_vm.SeciliLinye?.BaglantiId);
        cikisSec.SelectionChanged += (_, _) =>
        {
            if (cikisSec.SelectedItem is not SigortaYeri yer) return;
            var hesapHatasi = BasitProjeCizPanoServisi.SigortaKatalogHatalari(panolar[panoSec.SelectedIndex], yer.Id).FirstOrDefault();
            oneri.IsVisible = !yer.Yedek;
            oneri.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            oneri.Text = hesapHatasi ?? (yer.Oneri > 0 ? $"Hesaplanan sigorta   {yer.Oneri} A" : "Hesaplanan sigorta   —");
            amper.SelectedItem = yer.Kayit is { Amper: > 0 } ? yer.Kayit.Amper : yer.Oneri;
            uygula.IsEnabled = !_vm.ProjeKilitli && amper.SelectedItem is int;
            otomatik.IsEnabled = !_vm.ProjeKilitli && !yer.Yedek && hesapHatasi == null && yer.Kayit is { Manuel: true };
            sil.IsEnabled = !_vm.ProjeKilitli && yer.Kayit is { Amper: > 0 };
        };
        amper.SelectionChanged += (_, _) => uygula.IsEnabled = !_vm.ProjeKilitli && amper.SelectedItem is int;
        void Kaydet(int deger)
        {
            if (panoSec.SelectedIndex < 0 || cikisSec.SelectedItem is not SigortaYeri yer) return;
            _vm.PanoSigortasiniDuzenle(panolar[panoSec.SelectedIndex].Pano, yer.Id, deger);
            CikislariYenile(yer.Id);
        }
        uygula.Click += (_, _) => { if (amper.SelectedItem is int deger) Kaydet(deger); };
        sil.Click += (_, _) => Kaydet(0);
        otomatik.Click += (_, _) =>
        {
            if (panoSec.SelectedIndex < 0 || cikisSec.SelectedItem is not SigortaYeri yer) return;
            _vm.PanoSigortasiniOtomatigeAl(panolar[panoSec.SelectedIndex].Pano, yer.Id);
            CikislariYenile(yer.Id);
        };
        kapat.Click += (_, _) => dialog.Close();
        panoSec.SelectedIndex = Math.Max(0, panolar.FindIndex(o => o.Pano.Id == _vm.SeciliPanoOzet?.Pano.Id));
        await dialog.ShowDialog(this);
    }
}
