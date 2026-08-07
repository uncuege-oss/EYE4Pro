using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Architecture.Reports.ViewModels;
using Kst.Acoustics.App.Architecture.Views;
using Kst.Acoustics.Core.Architecture.Exporting;
using Kst.Acoustics.Core.Architecture.Reports;
using Kst.Acoustics.Core.Architecture.Services;

namespace Kst.Acoustics.App.Architecture.Reports.Views;

public partial class MimariCiktiWindow : Window
{
    private readonly Func<Window>? _geriPencere;

    public MimariCiktiWindow() : this(null)
    {
    }

    public MimariCiktiWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = new MimariCiktiViewModel();
        _geriPencere = geriPencere;
    }

    private MimariCiktiViewModel Vm => (MimariCiktiViewModel)DataContext!;

    private void OrnekProje_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.OrnekProjeOlustur();

    private async void ProjeAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Mimari Proje Aç",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("EYE4PRO Mimari") { Patterns = ["*.e4mimari.json", "*.json"] }]
            });

            if (dosyalar.Count == 0)
                return;

            var proje = MimariProjeServisi.Ac(dosyalar[0].Path.LocalPath);
            Vm.ProjeYukle(proje);
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Proje açılamadı: {ex.Message}";
        }
    }

    private async void HtmlKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await MetinKaydet(
            "HTML Rapor Kaydet", "html", "HTML", "text/html",
            () => MimariRaporHtmlBuilder.Olustur(MimariRaporUretimServisi.TumRaporuOlustur(Vm.Proje)));
    }

    private async void ObjKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await MetinKaydet(
            "OBJ Dışa Aktar", "obj", "Wavefront OBJ", "model/obj",
            () => MimariObjIhracatServisi.Olustur(Vm.Proje));
    }

    private async void XlsxKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var hedef = await KayitHedefi("XLSX Rapor Kaydet", "xlsx", "Excel Çalışma Kitabı",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            if (hedef is null)
                return;

            var icerik = MimariRaporXlsxBuilder.Olustur(MimariRaporUretimServisi.TumRaporuOlustur(Vm.Proje));
            await using var akis = await hedef.OpenWriteAsync();
            await akis.WriteAsync(icerik);
            Vm.DurumMesaji = $"XLSX kaydedildi: {hedef.Name}";
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"XLSX kaydedilemedi: {ex.Message}";
        }
    }

    private async void BinaUret_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var hedef = await KayitHedefi("Bina Geometrisi (.e4bina) Üret", "json", "EYE4PRO Bina", null,
                $"{DosyaAdi()}.e4bina.json");
            if (hedef is null)
                return;

            // Kullanıcı kayıt hedefini açıkça seçti → üzerine yazma onaylı.
            var sonuc = BinaGeometriDisaAktarimServisi.DisaAktar(Vm.Proje, hedef.Path.LocalPath, uzerineYaz: true);
            Vm.DurumMesaji = $"{sonuc.Mesaj} (revizyon {sonuc.GeometriRevizyon})";
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $".e4bina üretilemedi: {ex.Message}";
        }
    }

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(_geriPencere?.Invoke() ?? new ArchitectureLauncherWindow());

    private async Task MetinKaydet(string baslik, string uzanti, string turAdi, string? mime, Func<string> uret)
    {
        try
        {
            var hedef = await KayitHedefi(baslik, uzanti, turAdi, mime, $"{DosyaAdi()}.{uzanti}");
            if (hedef is null)
                return;

            await using var akis = await hedef.OpenWriteAsync();
            await using var yazici = new StreamWriter(akis, new System.Text.UTF8Encoding(false));
            await yazici.WriteAsync(uret());
            Vm.DurumMesaji = $"Kaydedildi: {hedef.Name}";
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Kaydedilemedi: {ex.Message}";
        }
    }

    private async Task<IStorageFile?> KayitHedefi(string baslik, string uzanti, string turAdi, string? mime, string? onerilenAd = null)
    {
        var tip = new FilePickerFileType(turAdi) { Patterns = [$"*.{uzanti}"] };
        if (mime is not null)
            tip.MimeTypes = [mime];

        return await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = baslik,
            SuggestedFileName = onerilenAd ?? $"{DosyaAdi()}.{uzanti}",
            DefaultExtension = uzanti,
            FileTypeChoices = [tip]
        });
    }

    private string DosyaAdi()
    {
        var ad = string.IsNullOrWhiteSpace(Vm.ProjeAdi) ? "Mimari-Proje" : Vm.ProjeAdi.Trim();
        foreach (var karakter in Path.GetInvalidFileNameChars())
            ad = ad.Replace(karakter, '-');
        return ad;
    }

    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            var w = (int)(double.IsNaN(win.Width) || win.Width < 100 ? 1200 : win.Width);
            var h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800 : win.Height);
            w = Math.Min(w, area.Width);
            h = Math.Min(h, area.Height);
            win.Position = new PixelPoint(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2);
        }
        else
        {
            win.Position = Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;

        win.Show();
        Close();
    }
}
