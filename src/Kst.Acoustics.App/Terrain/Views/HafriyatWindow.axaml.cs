using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

/// M60 — Hafriyat Hesapla penceresi. DXF arazi yükler, TIN'i izometrik gösterir,
/// hedef kota göre yarma/dolgu hesaplar ve .e4arazi.json olarak kaydeder.
/// MVP: tasarım bölgesi = tüm arazi alanı (sınır kutusu) tek platform.
public partial class HafriyatWindow : Window
{
    private readonly Func<Window>? _geriDon;
    private AraziModeli? _arazi;
    private AraziYuzeyi? _yuzey;

    // Avalonia AXAML loader için zorunlu.
    public HafriyatWindow() : this(null) { }

    public HafriyatWindow(Func<Window>? geriDon)
    {
        InitializeComponent();
        _geriDon = geriDon;
    }

    private async void DxfYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "DXF Arazi Dosyası Seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DXF Dosyaları") { Patterns = ["*.dxf"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        DurumText.Text = "DXF arazi yükleniyor ve TIN üretiliyor…";
        try
        {
            var (arazi, yuzey, uyarilar) = await Task.Run(() =>
            {
                var sonuc = new DxfAraziParser().Parse(path);
                var y = new AraziYuzeyServisi().Olustur(sonuc.Arazi);
                return (sonuc.Arazi, y, sonuc.Uyarilar);
            });

            _arazi = arazi;
            _yuzey = yuzey;
            Onizleme3D.HitIntensityMap = null; // önceki renk haritasını temizle
            Onizleme3D.Surfaces = Arazi3DGorunumServisi.YuzeyleriOlustur(yuzey);
            RaporText.Text = "Arazi yüklendi. Hedef kot girip Hesapla'ya basın.";

            string uyari = uyarilar.Count > 0 ? "  Uyarı: " + string.Join(" ", uyarilar) : "";
            DurumText.Text =
                $"{arazi.Noktalar.Count} nokta, {arazi.Konturlar.Count} kontur, " +
                $"{yuzey.Dugumler.Count} düğüm, {yuzey.Ucgenler.Count} üçgen.{uyari}";
        }
        catch (Exception ex)
        {
            DurumText.Text = "DXF yüklenemedi: " + ex.Message;
        }
    }

    private void Hesapla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_arazi is null || _yuzey is null || _yuzey.Ucgenler.Count == 0)
        {
            DurumText.Text = "Önce geçerli bir DXF arazi yükleyin.";
            return;
        }

        if (!Sayi(HedefKotBox.Text, out double hedefKot))
        {
            DurumText.Text = "Hedef kot geçersiz.";
            return;
        }
        Sayi(KabarmaBox.Text, out double kabarma);
        Sayi(SikismaBox.Text, out double sikisma);

        // Tasarım: arazi sınır kutusunu kapsayan tek platform.
        var sinir = SinirPoligonu(_yuzey);
        if (sinir is null)
        {
            DurumText.Text = "Arazi sınırı belirlenemedi.";
            return;
        }

        var tasarim = new TasarimYuzeyi();
        tasarim.Bolgeler.Add(new TasarimBolgesi
        {
            Ad = "Platform",
            Tip = TasarimBolgeTipi.Platform,
            HedefKotM = hedefKot,
            Poligon = sinir
        });
        _arazi.Tasarim = tasarim;

        double grid = GridAraligiSec(sinir);
        var sonuc = new HafriyatHesabiServisi { GridAraligiM = grid }.Hesapla(_yuzey, tasarim);
        var rapor = HafriyatRaporServisi.Olustur(sonuc,
            new HafriyatKatsayilari { KabarmaYuzdesi = kabarma, SikismaYuzdesi = sikisma });

        RaporText.Text = rapor.Ozet;

        // Önizlemeye platformu da ekle ve yarma/dolgu renk haritasını uygula.
        var yuzeyler = Arazi3DGorunumServisi.YuzeyleriOlustur(_yuzey);
        Onizleme3D.HitIntensityMap = Arazi3DGorunumServisi.HafriyatRenkHaritasi(_yuzey, tasarim);
        yuzeyler.AddRange(Arazi3DGorunumServisi.PlatformYuzeyleri(tasarim));
        Onizleme3D.Surfaces = yuzeyler;

        DurumText.Text =
            $"Hesaplandı (ızgara {grid:0.##} m): yarma {sonuc.YarmaHacmiM3:N0} m³, " +
            $"dolgu {sonuc.DolguHacmiM3:N0} m³, net {sonuc.NetHacimM3:N0} m³.";
    }

    private async void AraziKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_arazi is null)
        {
            DurumText.Text = "Kaydedilecek arazi yok.";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Arazi Kaydet",
            DefaultExtension = "e4arazi.json",
            SuggestedFileName = string.IsNullOrWhiteSpace(_arazi.AraziAdi) ? "arazi" : _arazi.AraziAdi,
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Arazi (.e4arazi.json)") { Patterns = ["*.e4arazi.json"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            AraziGeometriServisi.Kaydet(_arazi, path);
            DurumText.Text = $"Kaydedildi (revizyon {_arazi.GeometriRevizyon}): {path}";
        }
        catch (Exception ex)
        {
            DurumText.Text = "Kaydedilemedi: " + ex.Message;
        }
    }

    private void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_geriDon is null) { Close(); return; }
        OpenWindowOnSameScreen(_geriDon());
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private static bool Sayi(string? metin, out double deger)
    {
        deger = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;
        var s = metin.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out deger);
    }

    private static List<Point2D>? SinirPoligonu(AraziYuzeyi yuzey)
    {
        if (yuzey.Dugumler.Count == 0) return null;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var d in yuzey.Dugumler)
        {
            if (d.X < minX) minX = d.X;
            if (d.Y < minY) minY = d.Y;
            if (d.X > maxX) maxX = d.X;
            if (d.Y > maxY) maxY = d.Y;
        }
        if (maxX <= minX || maxY <= minY) return null;
        return new List<Point2D> { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) };
    }

    // Hücre sayısını ~150×150 ile sınırlamak için ızgara aralığı seçer.
    private static double GridAraligiSec(List<Point2D> sinir)
    {
        double genislik = sinir[1].X - sinir[0].X;
        double yukseklik = sinir[2].Y - sinir[1].Y;
        double enBuyuk = Math.Max(genislik, yukseklik);
        double grid = enBuyuk / 150.0;
        return grid < 0.5 ? 0.5 : grid;
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
        else
        {
            win.Position = this.Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }
}
