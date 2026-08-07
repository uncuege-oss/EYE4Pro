using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Architecture.IhtiyacProgrami.ViewModels;
using Kst.Acoustics.App.Architecture.Views;
using Kst.Acoustics.Core.Architecture.Reports;
using Kst.Acoustics.Core.Architecture.Services;

namespace Kst.Acoustics.App.Architecture.IhtiyacProgrami.Views;

public partial class IhtiyacProgramiWindow : Window
{
    private readonly Func<Window>? _geriPencere;

    public IhtiyacProgramiWindow() : this(null)
    {
    }

    public IhtiyacProgramiWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = new IhtiyacProgramiViewModel();
        _geriPencere = geriPencere;
    }

    private IhtiyacProgramiViewModel Vm => (IhtiyacProgramiViewModel)DataContext!;

    private void ProgramiUret_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.ProgramiYenile();

    private void YeniMahal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.YeniMahalEkle();

    private void KopyalaMahal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MahalGereksinimiSatiriViewModel satir })
            Vm.MahalKopyala(satir);
    }

    private void SilMahal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MahalGereksinimiSatiriViewModel satir })
            Vm.MahalSil(satir);
    }

    private async void ProjeAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Mimari Proje Aç",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("EYE4PRO Mimari") { Patterns = ["*.e4mimari.json", "*.json"] }
                ]
            });

            var dosya = dosyalar.Count > 0 ? dosyalar[0] : null;
            if (dosya is null)
                return;

            var proje = MimariProjeServisi.Ac(dosya.Path.LocalPath);
            Vm.ProjeYukle(proje);
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Proje açılamadı: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Vm.DosyaYolu))
        {
            await FarkliKaydet();
            return;
        }

        try
        {
            MimariProjeServisi.Kaydet(Vm.KaydaHazirla(), Vm.DosyaYolu);
            Vm.KayitTamamlandi(Vm.DosyaYolu);
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Proje kaydedilemedi: {ex.Message}";
        }
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await FarkliKaydet();

    private void Dogrula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Vm.DogrulamayiYenile();
        Vm.DurumMesaji = "Ön kontrol yenilendi.";
    }

    private async void HtmlRapor_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await RaporKaydet(
            "HTML İhtiyaç Programı Raporu Kaydet",
            RaporDosyaAdiOner("-ihtiyac-programi", "html"),
            "HTML Rapor",
            "*.html",
            () => IhtiyacProgramiRaporServisi.HtmlOlustur(Vm.KaydaHazirla()));

    private async void CsvAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await RaporKaydet(
            "CSV Mahal Listesi Kaydet",
            RaporDosyaAdiOner("-mahal-listesi", "csv"),
            "CSV Mahal Listesi",
            "*.csv",
            () => IhtiyacProgramiRaporServisi.CsvOlustur(Vm.KaydaHazirla()));

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(_geriPencere?.Invoke() ?? new ArchitectureLauncherWindow());

    private async Task FarkliKaydet()
    {
        try
        {
            var hedef = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Mimari Proje Kaydet",
                SuggestedFileName = DosyaAdiOner(),
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType("EYE4PRO Mimari") { Patterns = ["*.e4mimari.json"] }
                ]
            });

            if (hedef is null)
                return;

            MimariProjeServisi.Kaydet(Vm.KaydaHazirla(), hedef.Path.LocalPath);
            Vm.KayitTamamlandi(hedef.Path.LocalPath);
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Proje kaydedilemedi: {ex.Message}";
        }
    }

    private async Task RaporKaydet(
        string baslik,
        string onerilenDosyaAdi,
        string dosyaTipiAdi,
        string desen,
        Func<string> icerikUret)
    {
        try
        {
            var hedef = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = baslik,
                SuggestedFileName = onerilenDosyaAdi,
                FileTypeChoices =
                [
                    new FilePickerFileType(dosyaTipiAdi) { Patterns = [desen] }
                ]
            });

            if (hedef is null)
                return;

            await File.WriteAllTextAsync(hedef.Path.LocalPath, icerikUret(), Encoding.UTF8);
            Vm.DurumMesaji = $"Rapor kaydedildi: {Path.GetFileName(hedef.Path.LocalPath)}";
        }
        catch (Exception ex)
        {
            Vm.DurumMesaji = $"Rapor kaydedilemedi: {ex.Message}";
        }
    }

    private string DosyaAdiOner()
    {
        var ad = string.IsNullOrWhiteSpace(Vm.ProjeAdi) ? "Yeni-Mimari-Proje" : Vm.ProjeAdi.Trim();
        foreach (var karakter in Path.GetInvalidFileNameChars())
        {
            ad = ad.Replace(karakter, '-');
        }

        return $"{ad}.e4mimari.json";
    }

    private string RaporDosyaAdiOner(string ek, string uzanti)
    {
        var ad = string.IsNullOrWhiteSpace(Vm.ProjeAdi) ? "Yeni-Mimari-Proje" : Vm.ProjeAdi.Trim();
        foreach (var karakter in Path.GetInvalidFileNameChars())
        {
            ad = ad.Replace(karakter, '-');
        }

        return $"{ad}{ek}.{uzanti}";
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
