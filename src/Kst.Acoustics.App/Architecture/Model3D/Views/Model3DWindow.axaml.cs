using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Architecture.Model3D.ViewModels;
using Kst.Acoustics.App.Architecture.Views;
using Kst.Acoustics.Core.Architecture.Services;

namespace Kst.Acoustics.App.Architecture.Model3D.Views;

public partial class Model3DWindow : Window
{
    private readonly Func<Window>? _geriPencere;

    public Model3DWindow() : this(null)
    {
    }

    public Model3DWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = new Model3DViewModel();
        _geriPencere = geriPencere;
    }

    private Model3DViewModel Vm => (Model3DViewModel)DataContext!;

    private void ModelUret_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.ModeliYenidenUret();

    private void OrnekModel_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.OrnekModelOlustur();

    private void KamerayiSifirla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OnizlemeCanvas.KamerayiSifirla();

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
            OnizlemeCanvas.KamerayiSifirla();
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

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenWindowOnSameScreen(_geriPencere?.Invoke() ?? new ArchitectureLauncherWindow());

    private async Task FarkliKaydet()
    {
        try
        {
            var hedef = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Mimari 3D Model Kaydet",
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

    private string DosyaAdiOner()
    {
        var ad = string.IsNullOrWhiteSpace(Vm.ProjeAdi) ? "Yeni-Mimari-Proje" : Vm.ProjeAdi.Trim();
        foreach (var karakter in Path.GetInvalidFileNameChars())
        {
            ad = ad.Replace(karakter, '-');
        }

        return $"{ad}.e4mimari.json";
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
