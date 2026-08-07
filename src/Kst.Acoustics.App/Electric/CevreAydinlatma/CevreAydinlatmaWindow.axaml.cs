using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.App.Electric.CevreAydinlatma;

public partial class CevreAydinlatmaWindow : Window
{
    private readonly CevreAydinlatmaViewModel viewModel;

    private static readonly JsonSerializerOptions TekHatJsonAyarlari = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public CevreAydinlatmaWindow()
    {
        InitializeComponent();
        viewModel = new CevreAydinlatmaViewModel();
        DataContext = viewModel;
    }

    private void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new ElectricLauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }

    private async void DxfYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Park DXF Seç",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DXF") { Patterns = new[] { "*.dxf" } }
                }
            });

            var dosya = dosyalar.Count > 0 ? dosyalar[0] : null;
            if (dosya is null)
                return;

            viewModel.DosyadanYuzeyYukle(dosya.Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"DXF yüklenemedi: {ex.Message}";
        }
    }

    private async void AraziYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Arazi Seç (.e4arazi.json)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("EYE4PRO Arazi (.e4arazi.json)") { Patterns = new[] { "*.e4arazi.json" } }
                }
            });

            var dosya = dosyalar.Count > 0 ? dosyalar[0] : null;
            if (dosya is null)
                return;

            viewModel.AraziYukleVeKotAta(dosya.Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"Arazi yüklenemedi: {ex.Message}";
        }
    }

    private void YuzeydenGuzergah_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Kütüphanedeki ilk armatür/direk varsayılan atanır (yoksa null — aktarımda uyarı verilir).
        int? armaturId = viewModel.Proje.ArmaturKutuphanesi.Count > 0 ? viewModel.Proje.ArmaturKutuphanesi[0].Id : null;
        int? direkId = viewModel.Proje.DirekKutuphanesi.Count > 0 ? viewModel.Proje.DirekKutuphanesi[0].Id : null;
        viewModel.SecilenYuzeydenGuzergahEkle(aralikM: 25, kenarOfsetM: 0, direkTanimId: direkId, armaturTanimId: armaturId);
    }

    private void OtomatikYerlestir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (viewModel.Proje.Guzergahlar.Count == 0)
        {
            viewModel.DurumMesaji = "Önce güzergâh tanımlayın (güzergâh çizimi sonraki sürümde).";
            return;
        }
        viewModel.TumDirekleriYerlestir();
    }

    private async void ProjeAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Çevre Aydınlatma Projesi Aç",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Çevre Aydınlatma") { Patterns = new[] { "*.e4p15" } }
                }
            });

            var dosya = dosyalar.Count > 0 ? dosyalar[0] : null;
            if (dosya is null)
                return;

            viewModel.ProjeAc(dosya.Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"Proje açılamadı: {ex.Message}";
        }
    }

    private async void ProjeKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var dosya = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Çevre Aydınlatma Projesi Kaydet",
                DefaultExtension = "e4p15",
                SuggestedFileName = "cevre_aydinlatma",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Çevre Aydınlatma") { Patterns = new[] { "*.e4p15" } }
                }
            });

            if (dosya is null)
                return;

            viewModel.ProjeKaydet(dosya.Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"Proje kaydedilemedi: {ex.Message}";
        }
    }

    private async void TekHataAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sonuc = viewModel.TekHataAktarimiOlustur();
        if (!sonuc.Basarili || sonuc.Proje is null)
            return;

        try
        {
            var dosya = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Tek Hat'a Aktar",
                DefaultExtension = "e4p11",
                SuggestedFileName = "cevre_aydinlatma",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Tek Hat Şeması") { Patterns = new[] { "*.e4p11" } }
                }
            });

            if (dosya is null)
                return;

            string json = JsonSerializer.Serialize(sonuc.Proje, TekHatJsonAyarlari);
            File.WriteAllText(dosya.Path.LocalPath, json);
            viewModel.DurumMesaji = $"Tek hat aktarıldı: {dosya.Name}";
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"Aktarım yazılamadı: {ex.Message}";
        }
    }
}
