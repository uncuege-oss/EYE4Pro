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
using Kst.Acoustics.Core.Common;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.App.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.Views;

public partial class TekHatSemasiWindow
{
    // === PROJE AYARLARI (Selektivite + Gerilim Düşümü) ===

    private void KarAktif_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_karUiYukleniyor || _vm.SeciliPano == null || KarAktifToggle == null) return;
        if (_vm.SeciliPano.PanoTipi == PanoTipi.KontrolPanosu) return;

        _vm.SeciliPano.KacakAkimRolesiAktif = KarAktifToggle.IsChecked ?? false;
        _vm.TumunuHesapla();
        TabloGuncelle();
        SemaCiz();
    }

    private void Selektivite_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelektiviteToggle == null) return;
        _vm.Proje.SelektiviteAktif = SelektiviteToggle.IsChecked ?? false;
        _vm.TumunuHesapla();
        TabloGuncelle();
        SemaCiz();
    }

    private void GerilimDusus_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (NumberHelper.TryParse(GerilimDususBox?.Text, out double v) && v > 0 && v <= 100)
        {
            _vm.Proje.IzinVerilenGerilimDususYuzde = v;
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
        }
    }

    private void ToplamGerilimDusus_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (NumberHelper.TryParse(ToplamGerilimDususBox?.Text, out double v) && v > 0 && v <= 100)
        {
            _vm.Proje.IzinVerilenToplamGerilimDususYuzde = v;
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
        }
    }

    // === UPS TOPOLOJİSİ (Lokal / Merkezi) ===

    private bool _topolojiYukleniyor;

    private void UpsTopoloji_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_topolojiYukleniyor || UpsTopolojiCombo == null) return;
        var t = UpsTopolojiCombo.SelectedIndex == 1 ? UpsTopolojisi.Merkezi : UpsTopolojisi.Lokal;
        _vm.UpsTopolojisiAyarla(t);
        UpsTopolojiArayuzGuncelle();
        SemaCiz();
    }

    private void MerkeziUpsKur_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (_vm.MerkeziUpsVar) _vm.MerkeziUpsKaldir();
            else _vm.MerkeziUpsKur();
            UpsTopolojiArayuzGuncelle();
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();
            PanoOzetText.Text = _vm.MerkeziUpsVar
                ? "✓ Merkezi UPS kuruldu. Dağıtım panosuna sağ-tık → 'Kat Kritik Panosu Ekle'."
                : "Merkezi UPS kaldırıldı (dağıtım + kat kritik panoları tali panoya döndü).";
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ {ex.Message}";
        }
    }

    /// <summary>Toolbar topoloji seçicisini ve 'Merkezi UPS Kur/Kaldır' butonunu projeye göre tazeler.</summary>
    private void UpsTopolojiArayuzGuncelle()
    {
        if (UpsTopolojiCombo == null) return;
        _topolojiYukleniyor = true;
        UpsTopolojiCombo.SelectedIndex = _vm.MerkeziModda ? 1 : 0;
        _topolojiYukleniyor = false;
        if (MerkeziUpsKurBtn != null)
        {
            MerkeziUpsKurBtn.IsVisible = _vm.MerkeziModda;
            MerkeziUpsKurBtn.Content = _vm.MerkeziUpsVar ? "Merkezi UPS Kaldır" : "Merkezi UPS Kur";
        }
    }

    // === KÜTÜPHANE ===

    private void Kutuphane_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new KutuphaneWindow(_vm);
        win.Closed += (_, _) => {
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
        };
        win.Show();
    }

    // === BUSBAR ===

    private async void BusbarEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Panolar.Count == 0)
        {
            PanoOzetText.Text = "⚠ Önce en az bir ana pano oluşturun.";
            return;
        }
        var dlg = new BusbarEkleDialog(_vm);
        await dlg.ShowDialog(this);
        TabloGuncelle();
        SemaCiz();
        PanoSeciciGuncelle();
    }

    private ContextMenu BusbarContextMenu(BusbarHatti bb)
    {
        var ctx = new ContextMenu();

        var miDuzenle = new MenuItem { Header = "Busbarı Düzenle" };
        miDuzenle.Click += async (_, _) => await BusbarDuzenleAc(bb);

        var miSil = new MenuItem { Header = "Busbarı Sil" };
        miSil.Click += (_, _) =>
        {
            _vm.BusbarSil(bb);
            _vm.TumunuHesapla();
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();
            PanoOzetText.Text = $"✓ '{bb.Ad}' busbar hattı silindi; bağlı panolar kablo bağlantısına çevrildi.";
        };

        ctx.Items.Add(miDuzenle);
        ctx.Items.Add(new Separator());
        ctx.Items.Add(miSil);
        return ctx;
    }

    private async Task BusbarDuzenleAc(BusbarHatti bb)
    {
        var dlg = new BusbarEkleDialog(_vm, bb);
        await dlg.ShowDialog(this);
        _vm.TumunuHesapla();
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
        PanoOzetText.Text = $"✓ '{bb.Ad}' busbar hattı güncellendi.";
    }

    private async Task BusbarKutuDuzenleAc(BusbarHatti bb, Pano pano)
    {
        var dlg = new PanoBusbarBaglaDialog(_vm, pano, bb);
        await dlg.ShowDialog(this);
        _vm.TumunuHesapla();
        TabloGuncelle();
        PanoSeciciGuncelle();
        SemaCiz();
        var kutu = bb.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == pano.Ad);
        PanoOzetText.Text = kutu == null
            ? $"✓ '{pano.Ad}' busbar bağlantısı güncellendi."
            : $"✓ '{pano.Ad}' çıkış kutusu {kutu.PozisyonM:0.##} m konumuna ayarlandı.";
    }

    // === PROJE İŞLEMLERİ ===

    private async void YeniProje_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await DegisiklikleriKoruAsync(
                "Yeni Proje",
                "Yeni proje oluşturulursa mevcut kaydedilmemiş tek hat değişiklikleri kaybolacak.",
                "Kaydetmeden Yeni Proje",
                "Kaydet ve Yeni Proje"))
            return;

        _vm.ProjeyiYukle(new ElektrikProje());
        _vm.ProjeDosyaYolu = null;
        KatComboGuncelle();
        UpsTopolojiArayuzGuncelle();
        PanoSeciciGuncelle();
        TabloGuncelle();
        _ilkSigdirYapildi = false; // yeni içerik → görünümü yeniden sığdır
        SemaCiz();

        if (ProjeAdiText != null) ProjeAdiText.Text = "Yeni Proje";
        PanoOzetText.Text = "Yeni proje oluşturuldu. Lütfen üst çubuktan bir pano ekleyin.";
        KayitDurumunuTemizle();
    }

    private async void ProjeAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Eye4Pro Electric M11 Projesi Aç",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("EYE4PRO M11 Tek Hat (.e4p11)") { Patterns = new[] { "*.e4p11" } },
                FilePickerFileTypes.All
            }
        });
        if (files == null || files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        if (!await DegisiklikleriKoruAsync(
                "Başka Proje Aç",
                "Başka bir proje açılırsa mevcut kaydedilmemiş tek hat değişiklikleri kaybolacak.",
                "Kaydetmeden Aç",
                "Kaydet ve Aç"))
            return;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var proje = JsonSerializer.Deserialize<ElektrikProje>(json, _jsonOpts);
            if (proje == null) throw new Exception("Boş proje");
            _vm.ProjeyiYukle(proje); // KokPano re-link dahil tüm referansları kurar
            _vm.ProjeDosyaYolu = path;
            SelektiviteToggle.IsChecked = proje.SelektiviteAktif;
            GerilimDususBox.Text = proje.IzinVerilenGerilimDususYuzde.ToString("0.##");
            ToplamGerilimDususBox.Text = proje.IzinVerilenToplamGerilimDususYuzde.ToString("0.##");
            UpsTopolojiArayuzGuncelle();
            KatComboGuncelle();
            if (ProjeAdiText != null) ProjeAdiText.Text = Path.GetFileNameWithoutExtension(path);
            PanoSeciciGuncelle();
            TabloGuncelle();
            _ilkSigdirYapildi = false; // açılan proje → görünümü içeriğe yeniden sığdır
            SemaCiz();
            PanoOzetText.Text = $"✓ Proje açıldı: {path}";
            KayitDurumunuTemizle();
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ Proje açma hatası: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.ProjeDosyaYolu))
        {
            await FarkliKaydetAsync();
        }
        else
        {
            ProjeyiKaydet(_vm.ProjeDosyaYolu);
        }
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await FarkliKaydetAsync();
    }

    private async Task<bool> FarkliKaydetAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return false;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "EYE4PRO M11 Tek Hat Projesini Kaydet",
            SuggestedFileName = (_vm.Proje.ProjeAdi ?? "yeni_proje").Replace(' ', '_'),
            DefaultExtension = "e4p11",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("EYE4PRO M11 Tek Hat (.e4p11)") { Patterns = new[] { "*.e4p11" } }
            }
        });
        if (file == null) return false;
        var path = file.Path.LocalPath;
        if (!path.EndsWith(".e4p11", StringComparison.OrdinalIgnoreCase))
            path += ".e4p11";
        return ProjeyiKaydet(path);
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private bool ProjeyiKaydet(string path)
    {
        try
        {
            _vm.KaydetmeyeHazirla(); // UstPanoAd vb. referansları bellekteki ağaçla eşitle
            var json = JsonSerializer.Serialize(_vm.Proje, _jsonOpts);
            GuvenliDosyaYazmaServisi.Yaz(path, json);
            _vm.ProjeDosyaYolu = path;
            KayitDurumunuTemizle();
            if (ProjeAdiText != null) ProjeAdiText.Text = Path.GetFileNameWithoutExtension(path);
            PanoOzetText.Text = $"✓ Proje kaydedildi: {path}";
            return true;
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ Kaydetme hatası: {ex.Message}";
            return false;
        }
    }

    private async void ExcelDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Proje.TumPanolar.Count == 0)
        {
            PanoOzetText.Text = "⚠ Dışa aktarılacak pano yok. Önce bir tek hat şeması oluşturun.";
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Pano Listelerini Excel Olarak Dışa Aktar",
            SuggestedFileName = (_vm.Proje.ProjeAdi ?? "tek_hat").Replace(' ', '_') + "_pano_listeleri",
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel Çalışma Kitabı (.xlsx)") { Patterns = ["*.xlsx"] }
            ]
        });
        if (file == null) return;
        var path = file.Path.LocalPath;
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) path += ".xlsx";

        try
        {
            _vm.TumunuHesapla();
            TekHatSemasiExcelServisi.RaporOlustur(_vm.Proje, path);
            PanoOzetText.Text = $"✓ Pano listeleri Excel olarak kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ Excel dışa aktarma hatası: {ex.Message}";
        }
    }

    private async void DxfDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Proje.TumPanolar.Count == 0)
        {
            PanoOzetText.Text = "⚠ Dışa aktarılacak pano yok. Önce bir tek hat şeması oluşturun.";
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Tek Hat Şemasını DXF Olarak Dışa Aktar",
            SuggestedFileName = (_vm.Proje.ProjeAdi ?? "tek_hat").Replace(' ', '_') + "_tekhat",
            DefaultExtension = "dxf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("AutoCAD DXF (.dxf)") { Patterns = new[] { "*.dxf" } }
            }
        });
        if (file == null) return;
        var path = file.Path.LocalPath;
        if (!path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) path += ".dxf";

        try
        {
            _vm.TumunuHesapla();
            TekHatSemasiDxfServisi.DxfCiktiAl(_vm.Proje, path, _vm.Kutuphane);
            PanoOzetText.Text = $"✓ Tek hat şeması DXF olarak kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            PanoOzetText.Text = $"⚠ DXF dışa aktarma hatası: {ex.Message}";
        }
    }

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _launcheriAc = true;
        Close();
    }

    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_kapanisOnaylandi)
            return;

        if (!DegisiklikVarMi())
        {
            _kapanisOnaylandi = true;
            if (_launcheriAc)
                LauncherAc();
            return;
        }

        e.Cancel = true;
        var secim = await CikisOnayWindow.Goster(
            this,
            "Kaydedilmemiş Değişiklikler",
            "Tek hat projesinde kaydedilmemiş değişiklikler var. Çıkmadan önce kaydetmek ister misiniz?");
        if (secim == CikisSecimi.Iptal)
        {
            _launcheriAc = false;
            return;
        }
        if (secim == CikisSecimi.KaydetVeCik && !await KaydetVarsayilanAsync())
            return;

        _kapanisOnaylandi = true;
        if (_launcheriAc)
            LauncherAc();
        Close();
    }

    private void LauncherAc()
    {
        var launcher = new Kst.Acoustics.App.Views.ElectricLauncherWindow();
        var ekran = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (ekran is not null)
        {
            launcher.WindowStartupLocation = WindowStartupLocation.Manual;
            var alan = ekran.WorkingArea;
            var genislik = (int)(launcher.Width * ekran.Scaling);
            var yukseklik = (int)(launcher.Height * ekran.Scaling);
            launcher.Position = new Avalonia.PixelPoint(
                alan.X + Math.Max(0, (alan.Width - genislik) / 2),
                alan.Y + Math.Max(0, (alan.Height - yukseklik) / 2));
        }
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = launcher;
        launcher.Show();
    }

    private async Task<bool> KaydetVarsayilanAsync()
    {
        if (string.IsNullOrWhiteSpace(_vm.ProjeDosyaYolu))
            return await FarkliKaydetAsync();
        return ProjeyiKaydet(_vm.ProjeDosyaYolu);
    }

    private async Task<bool> DegisiklikleriKoruAsync(
        string baslik,
        string mesaj,
        string kaydetmedenMetni,
        string kaydetMetni)
    {
        if (!DegisiklikVarMi())
            return true;

        var secim = await CikisOnayWindow.Goster(
            this, baslik, mesaj, kaydetmedenMetni, kaydetMetni);
        return secim switch
        {
            CikisSecimi.KaydetmedenCik => true,
            CikisSecimi.KaydetVeCik => await KaydetVarsayilanAsync(),
            _ => false
        };
    }

    private bool DegisiklikVarMi()
        => !string.Equals(_kaydedilenProjeParmakIzi, ProjeParmakIziOlustur(), StringComparison.Ordinal);

    private void KayitDurumunuTemizle()
        => _kaydedilenProjeParmakIzi = ProjeParmakIziOlustur();

    private string ProjeParmakIziOlustur()
        => JsonSerializer.Serialize(_vm.Proje, _jsonOpts);

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        bool komut = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                     || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!komut || e.Key != Key.S)
            return;

        e.Handled = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            await FarkliKaydetAsync();
        else
            await KaydetVarsayilanAsync();
    }

    // === KOMPANZASYON KADEME YÖNETİMİ ===

    private bool _kompUiYukleniyor;

    private void KompKondKademeSayi_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        int sayi = KompKondKademeSayiCombo.SelectedIndex switch { 1 => 12, 2 => 18, _ => 6 };
        _vm.KompanzasyonKondansatorKademeSayisi(sayi);
        TabloGuncelle();
    }

    private void KompReaktorAktif_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        _vm.KompanzasyonReaktorAktifAyarla(KompReaktorToggle.IsChecked ?? false);
        TabloGuncelle();
    }

    private void KompAkort_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        double yuzde = KompAkortCombo.SelectedIndex switch { 0 => 5.67, 2 => 14.0, _ => 7.0 };
        _vm.KompanzasyonReaktorAkortAyarla(yuzde);
        TabloGuncelle();
    }

    private void KompKondansator_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        if (sender is ComboBox cb && cb.Tag is KompanzasyonKademe kademe
            && cb.SelectedItem is KondansatorTanim kond)
        {
            if (kademe.KondansatorAd == kond.Ad) return;
            _vm.KompanzasyonKondansatorKademesiniDegistir(kademe, kond);
            TabloGuncelle();
        }
    }

    private void KompReaktor_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        if (sender is ComboBox cb && cb.Tag is KompanzasyonReaktorKademe kademe
            && cb.SelectedItem is ReaktorTanim reaktor)
        {
            if (kademe.ReaktorAd == reaktor.Ad) return;
            _vm.KompanzasyonReaktorKademesiniDegistir(kademe, reaktor);
            TabloGuncelle();
        }
    }

    private void KompSvcSurucu_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        var sur = KompSvcSurucuCombo.SelectedItem as SvcSurucuTanim;
        _vm.KompanzasyonYukSurucuAyarla(sur);
        TabloGuncelle();
    }

    private void KompReaktorOzetToggle_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_kompUiYukleniyor) return;
        _vm.KompanzasyonReaktorAktifAyarla(KompReaktorOzetToggle.IsChecked ?? false);
        TabloGuncelle();
    }

    // === JENERATÖR ===

    private async void Jenerator_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Ana panoyu bul (varsa)
        var anaPano = _vm.Proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano);
        if (anaPano == null)
        {
            PanoOzetText.Text = "⚠ Jeneratör eklemek için önce bir Ana Pano oluşturun.";
            return;
        }

        var dlg = new JeneratorEkleDialog(_vm, anaPano);
        await dlg.ShowDialog(this);
        TabloGuncelle();
        SemaCiz();
        var grup = _vm.AktifJeneratorGrubu;
        PanoOzetText.Text = grup != null
            ? $"✓ Jeneratör grubu: {grup.Jeneratorler.Count} ünite, senkron pano {grup.SenkronPanoAd}"
            : _vm.JeneratorVar
            ? $"✓ Jeneratör: {_vm.AktifJenerator?.JeneratorTanimAd}"
            : "Jeneratör kaldırıldı.";
    }
}
