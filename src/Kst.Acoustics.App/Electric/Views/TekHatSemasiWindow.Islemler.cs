using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.App.Electric.ViewModels;

namespace Kst.Acoustics.App.Electric.Views;

public partial class TekHatSemasiWindow
{
    // === PANO İŞLEMLERİ ===

    private void YeniPanoTipi_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectedIndex: 0=Tali, 1=Ana, 2=Aydınlatma, 3=Kompanzasyon, 4=Kontrol, 5=Yangın
        if (YeniPanoAd != null)
        {
            bool anaPano = YeniPanoTipi?.SelectedIndex == 1;
            YeniPanoAd.IsEnabled = !anaPano;
            YeniPanoAd.PlaceholderText = anaPano ? "Ana Pano" : "Pano adı";
            if (anaPano) YeniPanoAd.Text = "";
        }
        if (AydinlatmaSayacliCheck != null)
            AydinlatmaSayacliCheck.IsVisible = YeniPanoTipi?.SelectedIndex == 2;
        if (HarmonikFiltreliCheck != null)
            HarmonikFiltreliCheck.IsVisible = YeniPanoTipi?.SelectedIndex == 3;
    }

    private async void Bina_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dlg = new BinaKonfigDialog(_vm.Proje.Bina);
        await dlg.ShowDialog(this);
        if (dlg.Onaylandi)
        {
            KatComboGuncelle();
            // Mevcut panoların kat ataması liste dışı kaldıysa otomatik düzelt
            var katlar = _vm.Proje.Bina.KatEtiketleri();
            foreach (var p in _vm.Panolar)
                if (!katlar.Contains(p.Kat)) p.Kat = katlar[0];
            TabloGuncelle();
            SemaCiz();
        }
    }

    private void PanoEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kat = YeniPanoKat?.SelectedItem as string ?? "Z";

        var tip = YeniPanoTipi.SelectedIndex switch
        {
            1 => PanoTipi.AnaPano,
            2 => PanoTipi.AydinlatmaPanosu,
            3 => PanoTipi.Kompanzasyon,
            4 => PanoTipi.KontrolPanosu,
            5 => PanoTipi.YanginPanosu,
            _ => PanoTipi.TaliPano
        };

        var ad = tip == PanoTipi.AnaPano ? "Ana Pano" : (YeniPanoAd?.Text?.Trim() ?? "");
        if (tip == PanoTipi.AnaPano && _vm.Panolar.Any(p => p.PanoTipi == PanoTipi.AnaPano))
        {
            PanoOzetText.Text = "⚠ Ana pano zaten var. Projede yalnızca 1 adet Ana Pano olabilir.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ad))
        {
            PanoOzetText.Text = "⚠ Pano adı boş olamaz! Üst çubuktaki 'Pano adı' alanını doldurun.";
            return;
        }

        bool sayacli = tip == PanoTipi.AydinlatmaPanosu && (AydinlatmaSayacliCheck?.IsChecked ?? false);
        bool harmonikFiltreli = tip == PanoTipi.Kompanzasyon && (HarmonikFiltreliCheck?.IsChecked ?? false);
        var harmonikFiltre = harmonikFiltreli ? _vm.HarmonikFiltreTipleri.FirstOrDefault() : null;

        _vm.YeniPanoEkle(ad, kat, tip, sayacli, harmonikFiltreli, harmonikFiltre);

        PanoSeciciGuncelle();

        var idx = _vm.Panolar.Count - 1;
        if (idx >= 0)
        {
            PanoSecici.SelectedIndex = idx;
            _vm.SeciliPano = _vm.Panolar[idx];
        }

        if (YeniPanoAd != null) YeniPanoAd.Text = "";
        // YeniPanoKat ComboBox — seçimi koru

        string mesaj = tip switch
        {
            PanoTipi.Kompanzasyon => $"✓ Kompanzasyon panosu '{ad}' eklendi. Bileşenler otomatik seçildi; sağ tık → Panoyu Düzenle ile değiştirebilirsiniz.",
            PanoTipi.AydinlatmaPanosu => $"✓ Aydınlatma panosu '{ad}' eklendi. Aydınlatma çıkışları (linye) eklediğinizde kontaktörler otomatik atanır.",
            PanoTipi.KontrolPanosu => $"✓ Kontrol panosu '{ad}' eklendi. Kontrol butonu ekleyerek darbe kesicili linyeleri seçebilirsiniz.",
            PanoTipi.YanginPanosu => $"✓ Yangın panosu '{ad}' eklendi. Ana şalter öncesinden beslenir; ana pano yüküne eklenmez.",
            _ => $"✓ Pano '{ad}' eklendi! Şimdi linye ekleyebilirsiniz."
        };
        PanoOzetText.Text = mesaj;
        TabloGuncelle();
        SemaCiz();
    }

    private void PanoSeciciGuncelle()
    {
        _panoIsimleri.Clear();
        foreach (var p in _vm.Panolar)
        {
            var prefix = p.PanoTipi switch
            {
                PanoTipi.AnaPano => "[ANA] ",
                PanoTipi.AydinlatmaPanosu => "[AYD] ",
                PanoTipi.Kompanzasyon => "[KOMP] ",
                PanoTipi.KontrolPanosu => "[KONTROL] ",
                PanoTipi.UpsPanosu => "[UPS] ",
                PanoTipi.TransferPanosu => "[TR] ",
                PanoTipi.YanginPanosu => "[YANGIN] ",
                _ => "[TALİ] "
            };
            var kw = p.ToplamGucW / 1000.0;
            var ozet = p.PanoTipi == PanoTipi.KontrolPanosu
                ? $"{p.KontrolButonlari.Count} buton"
                : $"{kw:N1} kW";
            _panoIsimleri.Add($"{prefix}{p.Ad} ({p.Kat}) — {ozet}");
        }
        var idx = _vm.Panolar.IndexOf(_vm.SeciliPano!);
        if (idx >= 0 && PanoSecici.SelectedIndex != idx)
            PanoSecici.SelectedIndex = idx;

        BagliBilgiGuncelle();
    }

    /// <summary>
    /// Seçili panonun bağlı olduğu üst pano (kablolu) veya busbar adını üst çubukta gösterir.
    /// </summary>
    private void BagliBilgiGuncelle()
    {
        if (BagliText == null) return;
        var p = _vm.SeciliPano;
        if (p == null)
        {
            BagliText.Text = "";
            return;
        }
        if (p.UstPano == null)
        {
            BagliText.Text = "Kaynak (üst pano yok)";
        }
        else if (p.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrEmpty(p.BaglandigiBusbarId))
        {
            var bb = _vm.Proje.TumBusbarlar.FirstOrDefault(b => b.Id == p.BaglandigiBusbarId);
            BagliText.Text = bb != null ? $"Busbar: {bb.Ad}" : $"Bağlı: {p.UstPano.Ad}";
        }
        else
        {
            BagliText.Text = $"Bağlı: {p.UstPano.Ad}";
        }
    }

    private void PanoSecim_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        var idx = PanoSecici.SelectedIndex;
        if (idx >= 0 && idx < _vm.Panolar.Count)
        {
            _vm.SeciliPano = _vm.Panolar[idx];
            TabloGuncelle();
            SemaCiz();
            BagliBilgiGuncelle();
        }
    }

    private async void KontrolButonuEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliPano?.PanoTipi != PanoTipi.KontrolPanosu)
        {
            PanoOzetText.Text = "Kontrol butonu eklemek için önce bir kontrol panosu seçin.";
            return;
        }

        var dlg = new KontrolButonuEkleDialog(_vm, _vm.SeciliPano);
        await dlg.ShowDialog(this);
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
    }

    private void KontrolButonuSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliPano?.PanoTipi != PanoTipi.KontrolPanosu) return;
        if (KontrolButonGrid.SelectedItem is not KontrolButonuAtama atama)
        {
            PanoOzetText.Text = "Silmek için kontrol butonu tablosundan bir satır seçin.";
            return;
        }

        _vm.KontrolButonuSil(_vm.SeciliPano, atama);
        TabloGuncelle();
        PanoSeciciGuncelle();
    }

    private void KontrolSinyalKabloGrid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (_vm.SeciliPano?.PanoTipi != PanoTipi.KontrolPanosu) return;
        var pano = _vm.SeciliPano;
        PanoOzetText.Text = $"Kontrol Butonu: {pano.KontrolButonlari.Count}   |   " +
                            $"Sinyal Kablosu: {pano.KontrolSinyalKablolari.Count} hat   |   " +
                            $"Damar: {pano.KontrolSinyalKablolari.Sum(k => k.DamarSayisi)}   |   " +
                            $"Metraj: {pano.KontrolSinyalKablolari.Sum(k => k.MetrajM):N1} m";
    }

    // === LİNYE İŞLEMLERİ ===

    private void YukTipi_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (YukTipiCombo.SelectedItem is YukTipiTanim tanim)
        {
            GucBox.Text = tanim.BirimGucW.ToString("0.##");
            if (tanim.Ad.Equals("Mekanik", StringComparison.OrdinalIgnoreCase) && FazTipiCombo != null)
                FazTipiCombo.SelectedIndex = 0;
        }
    }

    private void LinyeEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliPano == null)
        {
            PanoOzetText.Text = "⚠ Önce bir pano oluşturun! Üst çubuktan ad/kat girin ve '+ Pano Ekle' tıklayın.";
            return;
        }
        if (_vm.SeciliPano.PanoTipi == PanoTipi.KontrolPanosu)
        {
            PanoOzetText.Text = "Kontrol panosuna linye eklenmez; + Kontrol Butonu ile darbe kesicili linyeleri atayın.";
            return;
        }

        _vm.YeniLinyeNo = LinyeNoBox?.Text?.Trim() ?? "";
        _vm.YeniYukTipi = YukTipiCombo.SelectedItem as YukTipiTanim;
        _vm.YeniFazTipi = FazTipiCombo.SelectedIndex == 1 ? FazTipi.Trifaze : FazTipi.Monofaze;

        if (NumberHelper.TryParseInt(SortiBox?.Text, out int sorti)) _vm.YeniSortiSayisi = sorti;
        if (NumberHelper.TryParse(GucBox?.Text, out double guc)) _vm.YeniBirimGuc = guc;
        if (NumberHelper.TryParse(TalepBox?.Text, out double talep))
        {
            if (talep > 1.0)
            {
                PanoOzetText.Text = "⚠ Talep faktörü 1'den fazla olamaz!";
                return;
            }
            if (talep <= 0) talep = 1.0;
            _vm.YeniTalepFaktoru = talep;
        }
        _vm.YeniAciklama = "";

        _vm.LinyeEkle();

        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCizGecikmeli(); // şemayı toplu girişte tek seferde çiz (debounce)

        // Form temizle — sonraki ekleme için
        if (LinyeNoBox != null) LinyeNoBox.Text = "";
        if (SortiBox != null) SortiBox.Text = "1";
        if (TalepBox != null) TalepBox.Text = "1,0";
        if (FazTipiCombo != null) FazTipiCombo.SelectedIndex = 0;
        // Yük tipi varsayılan güce dön
        if (YukTipiCombo?.SelectedItem is YukTipiTanim yt2 && GucBox != null)
            GucBox.Text = yt2.BirimGucW.ToString("0.##");

        // Klavyeyle seri giriş için odağı Yük Tipi'ne geri ver
        YukTipiCombo?.Focus();
    }

    // Linye Ekle formunu aç/kapat
    private void LinyeToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LinyeFormFields == null) return;
        TogglePanelContent(LinyeFormFields, LinyeToggleBtn);
    }

    private static void TogglePanelContent(Control content, Button? toggleButton)
    {
        var acik = !content.IsVisible;
        content.IsVisible = acik;
        if (toggleButton != null) toggleButton.Content = acik ? "▾" : "▸";
    }

    private void PanoMenuToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (PanoMenuFields == null) return;
        TogglePanelContent(PanoMenuFields, PanoMenuToggleBtn);
    }

    private void LinyeExcelToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LinyeExcelFields == null) return;
        TogglePanelContent(LinyeExcelFields, LinyeExcelToggleBtn);
    }

    private void CikisSigortalariToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CikisSigortaGrid == null) return;
        TogglePanelContent(CikisSigortaGrid, CikisSigortalariToggleBtn);
    }

    // Linye satırına sağ tıklayınca o satırı seç (DataGrid sağ-tıkta otomatik seçmez).
    private void LinyeGrid_OnRightClickSelect(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(LinyeGrid);
        if (p.Properties.IsRightButtonPressed && (e.Source as Control)?.DataContext is Linye l)
            LinyeGrid.SelectedItem = l;
    }

    // Merkezi modda: seçili linyeyi aynı kattaki bir kritik panoya taşı (alt menüden seç).
    private void LinyeGrid_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (!_vm.MerkeziModda) return;
        var linye = LinyeGrid.SelectedItem as Linye;
        var kaynak = _vm.SeciliPano;
        if (linye == null || kaynak == null) return;

        var hedefler = _vm.KattakiKritikPanolar(kaynak.Kat).Where(p => p != kaynak).ToList();
        if (hedefler.Count == 0)
        {
            PanoOzetText.Text = $"⚠ '{kaynak.Kat}' katında kritik pano yok. Dağıtım panosuna 'Kat Kritik Panosu Ekle' ile ekleyin.";
            return;
        }

        var tasi = new MenuItem { Header = "🔋 Kritik Panoya Taşı" };
        foreach (var h in hedefler)
        {
            var hedef = h;
            var mi = new MenuItem { Header = $"{hedef.Ad}  ({hedef.Kat})" };
            mi.Click += (_, _) =>
            {
                _vm.LinyeyiKritikPanoyaTasi(linye, kaynak, hedef);
                TabloGuncelle();
                PanoSeciciGuncelle();
                SemaCiz();
                PanoOzetText.Text = $"✓ Linye '{hedef.Ad}' kritik panosuna taşındı.";
            };
            tasi.Items.Add(mi);
        }

        var ctx = new ContextMenu();
        ctx.Items.Add(tasi);
        ctx.Open(LinyeGrid);
        e.Handled = true;
    }

    // Linye "Kontaktör" sütunundaki tik: işaretlenince linyeye akıma göre kontaktör otomatik seçilir.
    private void LinyeKontaktor_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is Linye linye)
        {
            linye.KontaktorluMu = cb.IsChecked == true;
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
        }
    }

    // Linye "Darbe Kesici" sütunundaki tik: işaretlenince akıma göre darbe kesici seçilir;
    // akım 16A'i aşıyorsa ayrıca kontaktör de otomatik eklenir (PanoHesapServisi).
    private void LinyeDarbeKesici_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is Linye linye)
        {
            linye.DarbeKesiciMi = cb.IsChecked == true;
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
        }
    }

    // Linye "Cihaz" sütunu: kullanıcı Sigorta↔TMŞ seçti. Yalnızca ≤63A linyelerde anlamlı; >63A zaten TMŞ.
    // Programatik tablo yenilemelerinde seçim mevcut değerle aynı geldiğinden yok sayılır.
    private void LinyeCihaz_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.DataContext is not Linye linye) return;
        if (cb.SelectedItem is not string secim) return;
        bool yeniTms = secim == "TMŞ";
        if (yeniTms == linye.TmsZorla) return;          // değişmemiş (veya programatik) → yok say
        if (!linye.SigortaDegistirilebilir) return;      // >63A kilitli, zorunlu TMŞ
        linye.TmsZorla = yeniTms;
        linye.KesmeKapasitesiManuel = false;             // cihaz tipi değişti → kesme kapasitesi yeniden otomatik
        _vm.TumunuHesapla();
        TabloGuncelle();
        SemaCiz();
    }

    // Linye "kA" sütunu: kullanıcı kesme kapasitesini elle seçti → manuel işaretlenir, motor üzerine yazmaz.
    private void LinyeKesme_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.DataContext is not Linye linye) return;
        if (cb.SelectedItem is not string secim || string.IsNullOrEmpty(secim)) return;
        if (secim == linye.KesmeKapasitesi) return;      // değişmemiş (veya programatik) → yok say
        linye.KesmeKapasitesi = secim;
        linye.KesmeKapasitesiManuel = true;
        SemaCiz();
    }

    // Çıkış Sigortaları tablosu — "Cihaz" sütunu: alt panoya giden çıkış cihazını Sigorta↔TMŞ seç.
    private void CikisCihaz_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.DataContext is not Pano alt) return;
        if (cb.SelectedItem is not string secim) return;
        bool yeniTms = secim == "TMŞ";
        if (yeniTms == alt.CikisTmsZorla) return;
        if (!alt.CikisSigortaDegistirilebilir) return;
        alt.CikisTmsZorla = yeniTms;
        alt.CikisKesmeKapasitesiManuel = false;
        _vm.TumunuHesapla();
        TabloGuncelle();
        SemaCiz();
    }

    // Çıkış Sigortaları tablosu — "kA" sütunu: çıkış cihazı kesme kapasitesini elle seç.
    private void CikisKesme_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.DataContext is not Pano alt) return;
        if (cb.SelectedItem is not string secim || string.IsNullOrEmpty(secim)) return;
        if (secim == alt.CikisKesmeKapasitesi) return;
        alt.CikisKesmeKapasitesi = secim;
        alt.CikisKesmeKapasitesiManuel = true;
        SemaCiz();
    }

    // Hesap Özeti panelini aç/kapat (kapalıyken ince şerit + buton kalır, şemaya yer açılır)
    private void OzetToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OzetScroll == null || SagIcGrid == null) return;
        bool kapat = OzetScroll.IsVisible;
        if (kapat && SagIcGrid.ColumnDefinitions[2].ActualWidth > 80)
            _ozetPanelGenislik = SagIcGrid.ColumnDefinitions[2].ActualWidth;
        OzetScroll.IsVisible = !kapat;
        if (OzetGridSplitter != null) OzetGridSplitter.IsVisible = !kapat;
        SagIcGrid.ColumnDefinitions[1].Width = new GridLength(kapat ? 0 : 5);
        SagIcGrid.ColumnDefinitions[2].Width = new GridLength(kapat ? 34 : Math.Max(260, _ozetPanelGenislik));
        if (OzetToggleBtn != null) OzetToggleBtn.Content = kapat ? "◂" : "▸";
    }

    // Linye giriş formunda Enter → Ekle
    private void LinyeForm_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            LinyeEkle_OnClick(sender, e);
            e.Handled = true;
        }
    }

    // === LİNYE DÜZENLE / SİL ===

    private async void LinyeGrid_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (TextBoxKaynakliMi(e.Source)) return;
        await LinyeDuzenleAc();
    }

    private void LinyeGrid_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (TextBoxKaynakliMi(e.Source)) return;
        if (e.Key != Key.Delete && e.Key != Key.Back) return;
        if (SeciliLinyeyiSil())
            e.Handled = true;
    }

    private void LinyeAciklamaBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return) return;
        if (sender is not TextBox { DataContext: Linye linye }) return;

        e.Handled = true;
        AciklamaOdakSonrakiLinyeyeGec(linye);
    }

    private void AciklamaOdakSonrakiLinyeyeGec(Linye linye)
    {
        var linyeler = _vm.SeciliPano?.Linyeler;
        if (linyeler == null || linyeler.Count == 0) return;

        var index = linyeler.IndexOf(linye);
        if (index < 0) return;

        var sonraki = linyeler[Math.Min(index + 1, linyeler.Count - 1)];
        LinyeGrid.SelectedItem = sonraki;
        LinyeGrid.ScrollIntoView(sonraki, LinyeGrid.Columns.LastOrDefault());
        Dispatcher.UIThread.Post(() => AciklamaKutusuOdakla(sonraki), DispatcherPriority.Background);
    }

    private void AciklamaKutusuOdakla(Linye linye, int deneme = 0)
    {
        var kutu = LinyeGrid
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(t => t.Name == "LinyeAciklamaBox" && ReferenceEquals(t.DataContext, linye));

        if (kutu == null)
        {
            if (deneme < 3)
                Dispatcher.UIThread.Post(() => AciklamaKutusuOdakla(linye, deneme + 1), DispatcherPriority.Background);
            return;
        }

        kutu.Focus();
        kutu.CaretIndex = kutu.Text?.Length ?? 0;
    }

    private static bool TextBoxKaynakliMi(object? source)
    {
        if (source is TextBox) return true;
        return source is Control control && control.GetVisualAncestors().OfType<TextBox>().Any();
    }

    private async void LinyeDuzenle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LinyeDuzenleAc();
    }

    private async System.Threading.Tasks.Task LinyeDuzenleAc()
    {
        if (LinyeGrid.SelectedItem is not Linye linye) return;
        var dlg = new LinyeDuzenleDialog(_vm, linye);
        await dlg.ShowDialog(this);
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
    }

    private void LinyeTekrarlaEkleMenu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LinyeGrid.SelectedItem is not Linye linye) return;
        var yeni = _vm.LinyeTekrarlaEkle(linye);
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
        if (yeni != null)
        {
            LinyeGrid.SelectedItem = yeni;
            PanoOzetText.Text = $"✓ '{linye.LinyeNo}' linyesinden '{yeni.LinyeNo}' eklendi.";
        }
    }

    private void LinyeSilMenu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SeciliLinyeyiSil();
    }

    private bool SeciliLinyeyiSil()
    {
        if (LinyeGrid.SelectedItem is not Linye linye) return false;
        var silinenLinyeNo = linye.LinyeNo;
        _vm.LinyeSil(linye);
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
        PanoOzetText.Text = $"✓ '{silinenLinyeNo}' linyesi silindi.";
        return true;
    }

    private async void LinyeExcelSablon_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "M11 Linye Excel Şablonu Kaydet",
            SuggestedFileName = "m11_linye_aktarim_sablonu.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel Çalışma Kitabı (.xlsx)") { Patterns = ["*.xlsx"] }
            ]
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            path += ".xlsx";

        try
        {
            TekHatLinyeExcelServisi.SablonOlustur(path);
            PanoOzetText.Text = $"✓ Linye Excel şablonu kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ Excel şablonu oluşturma hatası: {ex.Message}";
        }
    }

    private async void LinyeExcelIceAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliPano == null)
        {
            PanoOzetText.Text = "⚠ Excelden aktarmak için önce bir pano seçin.";
            return;
        }

        if (_vm.SeciliPano.PanoTipi == PanoTipi.Kompanzasyon)
        {
            PanoOzetText.Text = "⚠ Kompanzasyon panosuna linye aktarılamaz.";
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seçili Panoya Linye Excelden Aktar",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Excel Çalışma Kitabı (.xlsx)") { Patterns = ["*.xlsx"] },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var (linyeler, hatalar) = TekHatLinyeExcelServisi.IceriAktar(path);
            var adet = _vm.LinyeleriIceAktar(linyeler);
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();

            var hataOzet = hatalar.Count > 0
                ? $" {hatalar.Count} uyarı: {string.Join(" | ", hatalar.Take(3))}"
                : "";
            PanoOzetText.Text = $"✓ {_vm.SeciliPano.Ad} panosuna {adet} linye aktarıldı.{hataOzet}";
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ Excelden aktarım hatası: {ex.Message}";
        }
    }

    // === KABLO / HESAPLA ===

    private void KabloMesafeAta_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliPano == null) return;
        if (NumberHelper.TryParse(KabloMesafe?.Text, out double mesafe))
        {
            _vm.SeciliPano.KabloMesafeM = mesafe;
            _vm.SeciliPano.KabloMesafeKullaniciAyarladi = true; // kullanıcı müdahale etti → Kontrol modunda sarı kalkar
            _vm.TumunuHesapla();
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();
            // Mesafe ata sonrası kutuyu temizle / varsayılana dön
            if (KabloMesafe != null) KabloMesafe.Text = "10";
        }
    }

    // === KONTROL (DOĞRULAMA) MODU ===

    // "Kontrol" butonu — modu aç/kapat. Açıkken panolar eksikliklerine göre renklenir.
    private void Kontrol_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _kontrolModu = !_kontrolModu;
        KontrolBtnGorunum();
        SemaCiz();
    }

    private void KontrolModunuKapat()
    {
        if (!_kontrolModu) return;
        _kontrolModu = false;
        KontrolBtnGorunum();
        SemaCiz();
    }

    private void KontrolBtnGorunum()
    {
        if (KontrolBtn != null)
        {
            KontrolBtn.Content = _kontrolModu ? "✓ Kontrol (Açık)" : "✓ Kontrol";
            KontrolBtn.Background = new SolidColorBrush(Color.Parse(_kontrolModu ? "#3A7A18" : "#162A10"));
            KontrolBtn.Foreground = new SolidColorBrush(Color.Parse(_kontrolModu ? "#FFFFFF" : "#A0C888"));
        }
        if (KontrolLegend != null) KontrolLegend.IsVisible = _kontrolModu;
    }

    // Bir panoda en az bir linyenin açıklaması boş mu?
    private static bool KontrolAciklamaEksik(Pano p) => p.Linyeler.Any(l => string.IsNullOrWhiteSpace(l.Aciklama));

    // Kablo mesafesi hâlâ varsayılan (kullanıcı ayarlamadı) mı? Yalnızca üst panosu olan panolarda anlamlı.
    private static bool KontrolKabloVarsayilan(Pano p) => p.UstPano != null && !p.KabloMesafeKullaniciAyarladi;

    // Kümülatif gerilim düşümü aşımı veya kablo kesiti yetersizliği (pano beslemesi ya da herhangi bir linye).
    private static bool KontrolGerilimVeyaKesitHatasi(Pano p) =>
        (p.UstPano != null && !p.KabloKesitiYeterli) || p.GerilimDususuAsiyor
        || p.Linyeler.Any(l => !l.KabloKesitiYeterli || l.GerilimDususuAsiyor);

    // Pano kontrol önem derecesi: 2=kırmızı (açıklama eksik / ΔU aşımı / kesit yetersiz),
    // 1=sarı (kablo varsayılan), 0=yeşil (tam).
    private static int PanoKontrolSeverity(Pano p)
    {
        if (KontrolAciklamaEksik(p) || KontrolGerilimVeyaKesitHatasi(p)) return 2;
        if (KontrolKabloVarsayilan(p)) return 1;
        return 0;
    }

    private static List<string> PanoEksiklikListesi(Pano p)
    {
        var liste = new List<string>();
        var aciklamasiz = p.Linyeler.Where(l => string.IsNullOrWhiteSpace(l.Aciklama)).ToList();
        if (aciklamasiz.Count > 0)
        {
            var noListe = aciklamasiz.Select(l => string.IsNullOrWhiteSpace(l.LinyeNo) ? "?" : l.LinyeNo);
            liste.Add($"🔴 Açıklama eksik: {aciklamasiz.Count} linye ({string.Join(", ", noListe)})");
        }
        if (p.UstPano != null && !p.KabloKesitiYeterli)
            liste.Add($"🔴 Besleme kablosu kesiti yetersiz ({p.KabloKesiti})");
        if (p.GerilimDususuAsiyor)
            liste.Add($"🔴 Toplam ΔU %{p.KumulatifGerilimDususYuzde:N2} — sınır aşıldı");
        int linyeKesitYetersiz = p.Linyeler.Count(l => !l.KabloKesitiYeterli);
        if (linyeKesitYetersiz > 0)
            liste.Add($"🔴 {linyeKesitYetersiz} linyenin kablo kesiti yetersiz");
        var asanLinye = p.Linyeler.Where(l => l.GerilimDususuAsiyor).ToList();
        if (asanLinye.Count > 0)
        {
            var noListe = asanLinye.Select(l => string.IsNullOrWhiteSpace(l.LinyeNo) ? "?" : l.LinyeNo);
            liste.Add($"🔴 Toplam ΔU sınırını aşan {asanLinye.Count} linye ({string.Join(", ", noListe)})");
        }
        if (KontrolKabloVarsayilan(p))
            liste.Add($"🟡 Kablo mesafesi varsayılan ({p.KabloMesafeM:N0} m) — kullanıcı ayarlamadı");
        return liste;
    }

    // Fareyle pano üzerine gelince görünen eksiklik tooltip'i.
    private static Control KontrolTooltip(Pano p)
    {
        var sp = new StackPanel { Spacing = 3 };
        sp.Children.Add(new TextBlock
        {
            Text = p.Ad,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Brushes.White,
            FontSize = 12
        });
        var eksikler = PanoEksiklikListesi(p);
        if (eksikler.Count == 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = "✓ Eksik yok",
                Foreground = new SolidColorBrush(Color.Parse("#7AD87A")),
                FontSize = 11
            });
        }
        else
        {
            foreach (var ek in eksikler)
                sp.Children.Add(new TextBlock
                {
                    Text = ek,
                    Foreground = new SolidColorBrush(Color.Parse("#E8D8B0")),
                    FontSize = 11,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 280
                });
        }
        return sp;
    }

    // Busbar etiketine fareyle (tıklamadan) gelince görünen özet: toplam yük + akım + nominal doluluk.
    private Control BusbarTooltip(BusbarHatti bb)
    {
        var bagli = _vm.Proje.TumPanolar.Where(p => p.BaglandigiBusbarId == bb.Id).ToList();
        double yukW = bagli.Sum(p => p.ToplamGucW);
        double yukVA = bagli.Sum(p => p.ToplamGorunurVA);
        var tanim = _vm.BusbarTipleri.FirstOrDefault(t => t.Ad == bb.BusbarTipiAd);
        int nominal = tanim?.NominalAkimA ?? 0;
        double doluluk = nominal > 0 ? bb.ToplamAkimA / nominal * 100.0 : 0;

        var sp = new StackPanel { Spacing = 3 };
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(bb.BusbarTipiAd) ? bb.Ad : $"{bb.Ad} • {bb.BusbarTipiAd}",
            FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Brushes.White, FontSize = 12
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"Toplam yük: {yukW / 1000.0:N1} kW ({yukVA / 1000.0:N1} kVA)",
            Foreground = new SolidColorBrush(Color.Parse("#CDE6B4")), FontSize = 11
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"Toplam akım: {bb.ToplamAkimA:N0} A",
            Foreground = new SolidColorBrush(Color.Parse("#CDE6B4")), FontSize = 11
        });
        sp.Children.Add(new TextBlock
        {
            Text = nominal > 0 ? $"Nominal: {nominal} A  •  doluluk %{doluluk:N0}" : "Nominal: —",
            Foreground = new SolidColorBrush(Color.Parse(doluluk > 100 ? "#E8A84A" : "#9CC080")), FontSize = 11
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"ΔU: %{bb.GerilimDususYuzde:N2}  •  {bagli.Count} çıkış  •  {bb.ToplamUzunlukM:N1} m",
            Foreground = new SolidColorBrush(Color.Parse("#9CC080")), FontSize = 11
        });
        return sp;
    }

}
