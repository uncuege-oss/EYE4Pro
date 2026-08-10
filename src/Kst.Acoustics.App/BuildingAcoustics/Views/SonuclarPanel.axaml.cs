using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class SonuclarPanel : UserControl
{
    private DataGridCollectionView? _sonucGorunumu;
    private bool _disaAktariliyor;

    public SonuclarPanel()
    {
        InitializeComponent();
        FilterBasarisiz.IsCheckedChanged += (_, _) => _sonucGorunumu?.Refresh();
        FilterVeriEksik.IsCheckedChanged += (_, _) => _sonucGorunumu?.Refresh();
        FilterDusukGuven.IsCheckedChanged += (_, _) => _sonucGorunumu?.Refresh();
        DataContextChanged += (_, _) => SonucGorunumunuBagla();
        SonucGorunumunuBagla();
    }

    private void SonucGorunumunuBagla()
    {
        if (DataContext is not BinaProjesiViewModel vm)
        {
            _sonucGorunumu = null;
            SonucDataGrid.ItemsSource = null;
            return;
        }

        _sonucGorunumu = new DataGridCollectionView(vm.SonucSatirlari)
        {
            Filter = SonucFiltresindenGecer,
        };
        SonucDataGrid.ItemsSource = _sonucGorunumu;
    }

    private bool SonucFiltresindenGecer(object item)
    {
        if (item is not SonucSatiriViewModel satir) return false;
        var basarisiz = FilterBasarisiz.IsChecked == true;
        var eksik = FilterVeriEksik.IsChecked == true;
        var dusukGuven = FilterDusukGuven.IsChecked == true;
        if (!basarisiz && !eksik && !dusukGuven) return true;

        return (basarisiz && satir.Uyum == UyumDurumu.Kaldi)
               || (eksik && satir.VeriEksik)
               || (dusukGuven && satir.GuvenPuani < 70);
    }

    private async void ExportReport_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel vm)
            return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
            return;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Bina Akustiği Raporu Kaydet",
            SuggestedFileName = $"{SanitizeFileName(vm.Proje.ProjeAdi)}-bina-akustigi-raporu.txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Metin Raporu") { Patterns = ["*.txt"] }
            ]
        });

        if (file == null)
            return;

        var projeAnlik = vm.DisaAktarmaAnlikGoruntusuAl();
        var haricTutulanlar = vm.RaporHaricTutulanBolmeIdleri;

        await DisaAktar("Metin raporu hazırlanıyor…", async () =>
        {
            var report = await Task.Run(() =>
                new BinaAkustikRaporServisi().MetinRaporOlustur(projeAnlik, haricTutulanlar));
            await MetinYaz(file, report);
        });
    }

    /// Yönetmelik Madde 5/5 yapısında yazdırılabilir HTML "Mimari Akustik Raporu".
    private async void ExportMimariAkustikRaporu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel vm) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var haricTutulanlar = vm.RaporHaricTutulanBolmeIdleri;
        var onKontrol = M08RaporUygulanabilirlikServisi.Degerlendir(vm.Proje, haricTutulanlar);
        if (vm.Proje.RaporAyarlari.YayinDurumu is M08RaporYayinDurumu.OnayaHazir or M08RaporYayinDurumu.Imzali &&
            onKontrol.YayinaEngelVar)
        {
            var engeller = string.Join("\n• ", onKontrol.Bulgular
                .Where(x => x.Seviye == RaporOnKontrolSeviyesi.Engel)
                .Select(x => x.Mesaj));
            var dialog = new OnayDiyaloguWindow(
                "Rapor Ön Kontrolü",
                $"Bu yayın durumunda rapor üretilemez:\n\n• {engeller}\n\nEksikleri tamamlayın veya yayın durumunu Taslak yapın.",
                tamamYazi: "Kapat", iptalYazi: "Kapat");
            await dialog.ShowDialog<OnaySonucu>(owner);
            return;
        }

        var belgeAdi = M08RaporUygulanabilirlikServisi.BelgeTuruAdi(vm.Proje.RaporAyarlari.BelgeTuru);
        var dosyaEtiketi = vm.Proje.RaporAyarlari.BelgeTuru switch
        {
            M08RaporBelgeTuru.AkustikProjeRaporPaketi => "akustik-proje-rapor-paketi",
            M08RaporBelgeTuru.SahaOlcumRaporu => "saha-olcum-raporu",
            M08RaporBelgeTuru.AkustikPerformansBelgesi => "akustik-performans-belgesi",
            _ => "mimari-akustik-raporu",
        };

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"{belgeAdi} Kaydet",
            SuggestedFileName = $"{SanitizeFileName(vm.Proje.ProjeAdi)}-{dosyaEtiketi}.html",
            FileTypeChoices = [ new FilePickerFileType("HTML Raporu") { Patterns = ["*.html"] } ]
        });
        if (file == null) return;

        var projeAnlik = vm.DisaAktarmaAnlikGoruntusuAl();
        await DisaAktar($"{belgeAdi} hazırlanıyor…", async () =>
        {
            var html = await Task.Run(() =>
                new MimariAkustikRaporServisi().HtmlOlustur(projeAnlik, haricTutulanlar));
            await MetinYaz(file, html);
        });
    }

    private async void ExportExcel_OnClick(object? sender, RoutedEventArgs e) =>
        await ExcelKaydet(BinaAkustikRaporServisi.RaporFiltresi.Tumu, "");

    private async void ExportExcelLimitAsan_OnClick(object? sender, RoutedEventArgs e) =>
        await ExcelKaydet(BinaAkustikRaporServisi.RaporFiltresi.SadeceLimitAsan, "-limit-asan");

    private async void ExportExcelDusukGuven_OnClick(object? sender, RoutedEventArgs e) =>
        await ExcelKaydet(BinaAkustikRaporServisi.RaporFiltresi.SadeceDusukGuven, "-dusuk-guven");

    private async System.Threading.Tasks.Task ExcelKaydet(BinaAkustikRaporServisi.RaporFiltresi filtre, string suffix)
    {
        if (DataContext is not BinaProjesiViewModel vm) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Excel Raporu Kaydet",
            SuggestedFileName = $"{SanitizeFileName(vm.Proje.ProjeAdi)}-bina-akustigi{suffix}.xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel Çalışma Kitabı") { Patterns = ["*.xlsx"] }
            ]
        });
        if (file == null) return;
        var path = file.Path.LocalPath;
        var projeAnlik = vm.DisaAktarmaAnlikGoruntusuAl();
        var haricTutulanlar = vm.RaporHaricTutulanBolmeIdleri;
        await DisaAktar("Excel çalışma kitabı hazırlanıyor…", () => Task.Run(() =>
            new BinaAkustikRaporServisi().ExcelRaporOlustur(
                projeAnlik, path, filtre, haricTutulanlar)));
    }

    private async void ExportDxf_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel vm) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Renkli DXF Rapor",
            SuggestedFileName = $"{SanitizeFileName(vm.Proje.ProjeAdi)}-renkli-rapor.dxf",
            FileTypeChoices = [ new FilePickerFileType("DXF") { Patterns = ["*.dxf"] } ]
        });
        if (file == null) return;
        var projeAnlik = vm.DisaAktarmaAnlikGoruntusuAl();
        var haricTutulanlar = vm.RaporHaricTutulanBolmeIdleri;
        await DisaAktar("Renkli DXF hazırlanıyor…", () => Task.Run(() =>
            new DxfRenkliCiktiServisi().DxfRenkliRaporOlustur(
                projeAnlik, file.Path.LocalPath, haricTutulanlar)));
    }

    private async Task DisaAktar(string baslangicMesaji, Func<Task> islem)
    {
        if (_disaAktariliyor) return;
        _disaAktariliyor = true;
        ExportToolbar.IsEnabled = false;
        ExportStatusBorder.IsVisible = true;
        ExportProgress.IsVisible = true;
        ExportStatusBorder.Background = Brush.Parse("#102A38");
        ExportStatusBorder.BorderBrush = Brush.Parse("#28708A");
        ExportStatusText.Foreground = Brush.Parse("#A8E8FF");
        ExportStatusText.Text = baslangicMesaji;

        try
        {
            await islem();
            ExportProgress.IsVisible = false;
            ExportStatusBorder.Background = Brush.Parse("#123525");
            ExportStatusBorder.BorderBrush = Brush.Parse("#2F8A55");
            ExportStatusText.Foreground = Brush.Parse("#A8FFD0");
            ExportStatusText.Text = "Dışa aktarım tamamlandı.";
        }
        catch (Exception ex)
        {
            ExportProgress.IsVisible = false;
            ExportStatusBorder.Background = Brush.Parse("#3A1717");
            ExportStatusBorder.BorderBrush = Brush.Parse("#A84848");
            ExportStatusText.Foreground = Brush.Parse("#FFC0C0");
            ExportStatusText.Text = $"Dışa aktarım başarısız: {ex.Message}";

            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new OnayDiyaloguWindow(
                    "Dışa Aktarım Başarısız",
                    $"Dosya oluşturulamadı.\n\n{ex.Message}",
                    tamamYazi: "Kapat",
                    iptalYazi: "Kapat");
                await dialog.ShowDialog<OnaySonucu>(owner);
            }
        }
        finally
        {
            ExportToolbar.IsEnabled = true;
            _disaAktariliyor = false;
        }
    }

    private static async Task MetinYaz(IStorageFile file, string metin)
    {
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek) stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(metin);
        await writer.FlushAsync();
    }

    private static string SanitizeFileName(string value)
    {
        var fallback = string.IsNullOrWhiteSpace(value) ? "proje" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            fallback = fallback.Replace(invalid, '-');
        return fallback;
    }
}
