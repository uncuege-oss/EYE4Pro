using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Landscape.Services;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Landscape.Views;

/// M70 — Park Tasarımı penceresi. Arazi (.e4arazi) ve bina (.e4bina) altlıklarını
/// salt-okunur yükler, bina+setback dışlamasıyla kullanılabilir alanı çıkarır,
/// 2D plan + alan/oran raporu gösterir, .e4p70 kaydeder ve .e4park.json paylaşır.
/// MVP: sert zemin öğelerini interaktif çizim F8'dedir; yüklenen .e4p70 öğeleri çizilir.
public partial class ParkTasarimWindow : Window
{
    private readonly Func<Window>? _geriDon;
    private ParkModeli _park = ParkGeometriServisi.YeniOlustur("Park");
    private AraziTuketim? _arazi;

    public ParkTasarimWindow() : this(null) { }

    public ParkTasarimWindow(Func<Window>? geriDon)
    {
        InitializeComponent();
        _geriDon = geriDon;
    }

    private async void AraziYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Arazi Seç", "EYE4PRO Arazi (.e4arazi.json)", "*.e4arazi.json");
        if (path is null) return;
        try
        {
            _arazi = ParkAltlikServisi.AraziYukle(_park, path);
            ParkAltlikServisi.ParkSiniriAraziden(_park, _arazi);
            ParkYerlesimServisi.ZeminKotlariniAta(_park, _arazi);
            Yenile();
            var aralik = _arazi.KotAraligi();
            DurumText.Text = $"Arazi yüklendi (rev {_arazi.Revizyon}, {_arazi.UcgenSayisi} üçgen" +
                             (aralik is { } a ? $", kot {a.Min:N1}–{a.Max:N1} m" : "") + ").";
        }
        catch (Exception ex) { DurumText.Text = "Arazi yüklenemedi: " + ex.Message; }
    }

    private async void BinaYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Bina Seç", "EYE4PRO Bina (.e4bina.json)", "*.e4bina.json");
        if (path is null) return;
        if (!Sayi(SetbackBox.Text, out double setback) || setback < 0) setback = 0;
        try
        {
            var d = ParkAltlikServisi.BinaYukle(_park, path, setback);
            Yenile();
            DurumText.Text = $"Bina '{d.BinaAdi}' yüklendi (setback {setback:N1} m). Toplam {_park.BinaDislamalari.Count} bina.";
        }
        catch (Exception ex) { DurumText.Text = "Bina yüklenemedi: " + ex.Message; }
    }

    private void Yenile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Yenile();

    private void Yenile()
    {
        var alan = ParkAlanServisi.KullanilabilirAlan(_park);
        var ozet = ParkOranServisi.Hesapla(_park, _arazi);

        Onizleme.Sinir = _park.Sinir.Poligon;
        Onizleme.Halkalar = alan.Halkalar;
        Onizleme.Ogeler = _park.Ogeler;

        var sb = new StringBuilder();
        sb.AppendLine($"Park: {_park.ParkAdi}");
        sb.AppendLine($"Toplam park alanı: {ozet.ToplamParkM2:N1} m²");
        sb.AppendLine($"Kullanılabilir (− bina/setback): {ozet.KullanilabilirM2:N1} m²");
        sb.AppendLine($"Sert zemin: {ozet.SertZeminM2:N1} m²  (%{ozet.SertZeminOran * 100:N1})");
        sb.AppendLine($"Yeşil alan: {ozet.YesilAlanM2:N1} m²  (%{ozet.YesilOran * 100:N1})");
        sb.AppendLine($"Bina dışlama: {_park.BinaDislamalari.Count} adet · Öğe: {_park.Ogeler.Count}");
        if (ozet.Uyarilar.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Uyarılar:");
            foreach (var u in ozet.Uyarilar) sb.AppendLine("• " + u);
        }
        RaporText.Text = sb.ToString().TrimEnd();
    }

    private async void ParkAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Park Aç", "EYE4PRO Park (.e4p70)", "*.e4p70");
        if (path is null) return;
        try
        {
            _park = ParkGeometriServisi.Ac(path);
            _arazi = null; // altlık yeniden yüklenebilir
            Yenile();
            DurumText.Text = $"Park açıldı (rev {_park.GeometriRevizyon}): {_park.Ogeler.Count} öğe.";
        }
        catch (Exception ex) { DurumText.Text = "Park açılamadı: " + ex.Message; }
    }

    private async void ParkKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaKaydet("Park Kaydet", "EYE4PRO Park (.e4p70)", "*.e4p70", "e4p70",
            string.IsNullOrWhiteSpace(_park.ParkAdi) ? "park" : _park.ParkAdi);
        if (path is null) return;
        try
        {
            ParkGeometriServisi.Kaydet(_park, path);
            DurumText.Text = $"Kaydedildi (rev {_park.GeometriRevizyon}): {path}";
        }
        catch (Exception ex) { DurumText.Text = "Kaydedilemedi: " + ex.Message; }
    }

    private async void Paylas_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaKaydet("Park Paylaş", "EYE4PRO Park Artifact (.e4park.json)", "*.e4park.json",
            "e4park.json", string.IsNullOrWhiteSpace(_park.ParkAdi) ? "park" : _park.ParkAdi);
        if (path is null) return;
        try
        {
            ParkGeometriServisi.ArtifactYaz(_park, path);
            DurumText.Text = $"Paylaşıldı (.e4park.json, rev {_park.GeometriRevizyon}): {path}";
        }
        catch (Exception ex) { DurumText.Text = "Paylaşılamadı: " + ex.Message; }
    }

    private void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_geriDon is null) { Close(); return; }
        OpenWindowOnSameScreen(_geriDon());
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task<string?> DosyaSec(string baslik, string ad, string desen)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = baslik,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(ad) { Patterns = [desen] }]
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async System.Threading.Tasks.Task<string?> DosyaKaydet(
        string baslik, string ad, string desen, string uzanti, string onerilen)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = baslik,
            DefaultExtension = uzanti,
            SuggestedFileName = onerilen,
            FileTypeChoices = [new FilePickerFileType(ad) { Patterns = [desen] }]
        });
        return file?.Path.LocalPath;
    }

    private static bool Sayi(string? metin, out double deger)
    {
        deger = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;
        return double.TryParse(metin.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out deger);
    }

    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = (int)(double.IsNaN(win.Width) || win.Width < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800 : win.Height);
            w = Math.Min(w, area.Width);
            h = Math.Min(h, area.Height);
            win.Position = new PixelPoint(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2);
        }
        else win.Position = this.Position;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }
}
