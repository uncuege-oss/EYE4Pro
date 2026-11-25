using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.App.Terrain.Rendering;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

/// <summary>Çok bölgeli arazi tesviyesi: kot, kenar şevi/dik kesim, önizleme ve hafriyat.</summary>
public partial class HafriyatWindow : Window
{
    private readonly Func<Window>? _geriDon;
    private AraziModeli? _arazi;
    private AraziYuzeyi? _yuzey;
    private string? _acikProjeYolu;
    private AraziKotIndeksi? _kotIndeksi;
    private AraziTesviyeServisi? _tesviye;
    private TerrainMesh? _mevcutAg;
    private sealed record Onizleme(TerrainMesh Ag, double OrneklemeAraligiM, IReadOnlyList<string> Uyarilar);
    private Onizleme? _onizleme;
    private int[]? _renkler;
    private bool _mesgul, _kuruluyor = true;
    private readonly CancellationTokenSource _kapanis = new();

    public HafriyatWindow() : this(null) { }
    public HafriyatWindow(Func<Window>? geriDon)
    {
        InitializeComponent(); _geriDon = geriDon;
        Plan.KonumDegisti = s => KonumText.Text = s;
        Plan.CizimDegisti = () => KonumText.Text = $"{Plan.CizimNoktalari.Count} köşe seçildi. Tamamla ile bölgeyi oluşturun.";
        Plan.BolgeSecildi = id => BolgeSec(id);
        Plan.KenarSecildi = (id, kenar) => BolgeSec(id, kenar);
        Plan.SinirDegisti = async (id, noktalar) => await SinirDuzenleAsync(id, noktalar);
        Plan.KotSecildi = kot => { HedefKotBox.Text = kot.ToString("0.###", CultureInfo.InvariantCulture); DurumText.Text = $"Araziden {kot:0.###} m alındı. Bölgeyi Uygula ile hedef kota geçirin."; };
        Opened += (_, _) =>
        {
            var ekran = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (ekran != null && WindowState == WindowState.Normal && WindowStartupLocation == WindowStartupLocation.CenterScreen &&
                (Width > ekran.WorkingArea.Width / ekran.Scaling || Height + 40 > ekran.WorkingArea.Height / ekran.Scaling))
                WindowState = WindowState.Maximized;
        };
        Closed += (_, _) => { _kapanis.Cancel(); Onizleme3D.Dispose(); };
        _kuruluyor = false; GecmisButonlari();
    }

    private async void DxfYukle_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || !BekleyenFormYok()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "DWG / DXF Arazi Dosyası Seç", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CAD Arazi Dosyaları") { Patterns = ["*.dwg", "*.dxf"] }] });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await CadYukleAsync(path);
    }
    public async Task CadYukleAsync(string path)
    {
        if (_mesgul) return;
        Mesgul(true, "CAD dosyası okunuyor…");
        try
        {
            var proje = await AraziProjeOkuyucu.AcAsync(this, path, _kapanis.Token);
            if (proje == null) { DurumText.Text = "İçe aktarma iptal edildi."; return; }
            await AraziGosterAsync(proje.Model, proje.Yuzey, path);
            ImportText.Text = KaynakOzeti(proje.Model) + $"{proje.Model.Noktalar.Count} kot noktası, {proje.Model.Konturlar.Count} kontur\n" + string.Join("\n", proje.Uyarilar);
            DurumText.Text = "Mevcut arazi hazır. + Bölge Çiz veya + Dikdörtgen ile başlayın.";
        }
        catch (Exception ex) { DurumText.Text = "Arazi yüklenemedi: " + ex.Message; }
        finally { Mesgul(false); }
    }

    private async void AraziAc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || !BekleyenFormYok()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Arazi Projesi Aç", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Arazi") { Patterns = ["*.e4arazi.json", "*.e4p60"] }] });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await AraziYukleAsync(path);
    }
    public async Task AraziYukleAsync(string path)
    {
        if (_mesgul) return;
        Mesgul(true, "Arazi ve tasarım yükleniyor…");
        try
        {
            var proje = await AraziProjeOkuyucu.AcAsync(this, path, _kapanis.Token);
            if (proje == null) { DurumText.Text = "İçe aktarma iptal edildi."; return; }
            var arazi = proje.Model;
            await AraziGosterAsync(arazi, proje.Yuzey, path);
            ImportText.Text = KaynakOzeti(arazi) + $"{arazi.Konturlar.Count:N0} kontur · Revizyon {arazi.GeometriRevizyon}";
            DurumText.Text = $"{arazi.Tasarim.Bolgeler.Count} tasarım bölgesi yüklendi. Mevcut zemin korunuyor.";
        }
        catch (Exception ex) { DurumText.Text = "Arazi açılamadı: " + ex.Message; }
        finally { Mesgul(false); }
    }
    private async Task AraziGosterAsync(AraziModeli arazi, AraziYuzeyi yuzey, string path)
    {
        var hazir = await Task.Run(() => {
            var ozet = AraziTasarimServisi.Ozetle(yuzey);
            var indeks = new AraziKotIndeksi(yuzey);
            var tesviye = new AraziTesviyeServisi(indeks, arazi.Tasarim);
            return (ozet, tesviye, ag: TerrainMesh.Measured(yuzey), indeks);
        }, _kapanis.Token);
        _kuruluyor = true;
        try
        {
            _arazi = arazi; _yuzey = yuzey; _acikProjeYolu = path; _tesviye = hazir.tesviye; _kotIndeksi = hazir.indeks;
            _mevcutAg = hazir.ag; _onizleme = null; _renkler = null;
            Onizleme3D.Scene(_mevcutAg, null, false, reset: true);
            _geri.Clear(); _ileri.Clear(); _formKirli = false; _seciliId = null; _seciliKenar = -1;
            Plan.Yukle(yuzey); Plan.Tasarim = arazi.Tasarim;
            DosyaText.Text = string.IsNullOrWhiteSpace(arazi.AraziAdi) ? Path.GetFileName(path) : arazi.AraziAdi;
            OzetText.Text = $"{yuzey.Dugumler.Count:N0} düğüm · {yuzey.Ucgenler.Count:N0} üçgen · Kot {hazir.ozet.MinKotM:0.00} – {hazir.ozet.MaxKotM:0.00} m";
            GridBox.Text = Math.Max(.1, Math.Min(2, GridAraligiSec(hazir.ozet.SinirPoligonu))).ToString("0.###", CultureInfo.InvariantCulture);
            _hesapImzasi = HesapImzasi();
            YuzeyGorunumBox.SelectedIndex = 0;
            BolgeListesiniGuncelle(arazi.Tasarim.Bolgeler.FirstOrDefault()?.Id);
            SonucuTemizle();
        }
        finally { _kuruluyor = false; }
        GorunumuUygula(); GecmisButonlari();
        if (arazi.Tasarim.Bolgeler.Count > 0) await OnizlemeHazirlaAsync();
    }

    private async void AraziKaydet_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_arazi == null || _mesgul || !BekleyenFormYok()) return;
        if (Plan.CizimModu) { DurumText.Text = "Kaydetmeden önce bölge çizimini tamamlayın veya iptal edin."; return; }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Arazi ve Çok Bölgeli Tasarımı Kaydet", DefaultExtension = "e4arazi.json",
            SuggestedFileName = (_arazi.AraziAdi.Length == 0 ? "arazi" : _arazi.AraziAdi) + "-tasarim.e4arazi.json",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Arazi") { Patterns = ["*.e4arazi.json"] }] });
        var path = file?.TryGetLocalPath(); if (string.IsNullOrWhiteSpace(path)) return;
        try { AraziGeometriServisi.Kaydet(_arazi, path); DurumText.Text = $"Arazi ve {_arazi.Tasarim.Bolgeler.Count} tasarım bölgesi kaydedildi: {path}"; }
        catch (Exception ex) { DurumText.Text = "Kaydedilemedi: " + ex.Message; }
    }
    private async void KotlariKaydet_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_yuzey == null || _mesgul) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Mevcut Arazi Kotları", DefaultExtension = "csv", SuggestedFileName = "mevcut-arazi-kotlari.csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }] });
        var path = file?.TryGetLocalPath(); if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var sb = new StringBuilder("No;X_m;Y_m;Z_m\n"); int i = 1;
            foreach (var p in _yuzey.Dugumler) sb.AppendLine(FormattableString.Invariant($"{i++};{p.X:0.######};{p.Y:0.######};{p.Z:0.######}"));
            await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
            DurumText.Text = "Mevcut arazi kotları kaydedildi.";
        }
        catch (Exception ex) { DurumText.Text = "Kotlar kaydedilemedi: " + ex.Message; }
    }

    private void Mesgul(bool value, string? mesaj = null)
    {
        _mesgul = value;
        DosyaAraclari.IsEnabled = PlanAraclari.IsEnabled = TasarimPanel.IsEnabled = HesapAyarlari.IsEnabled = HesaplaButton.IsEnabled = YuzeyGorunumBox.IsEnabled = Plan.IsEnabled = !value;
        if (mesaj != null) DurumText.Text = mesaj;
        if (!value) GecmisButonlari();
    }
    private static bool Sayi(string? s, out double d) =>
        double.TryParse(s?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) && double.IsFinite(d);
    private static string KaynakOzeti(AraziModeli arazi) => arazi.Kaynak is { } k
        ? $"Kaynak CAD: {Path.GetFileName(k.DosyaYolu)}\nÖlçek: 1 birim = {k.MetrePerBirim:G} m\nKatmanlar: {string.Join(", ", k.Katmanlar)}\n" : "";
    private static double GridAraligiSec(IReadOnlyList<Point2D> sinir) =>
        Math.Max(.05, Math.Max(sinir.Max(p => p.X) - sinir.Min(p => p.X), sinir.Max(p => p.Y) - sinir.Min(p => p.Y)) / 120);
    private async void Karsilastir_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || !BekleyenFormYok()) return;
        var ilk = _arazi != null && _yuzey != null && _acikProjeYolu != null
            ? new YuklenenArazi(_acikProjeYolu, _arazi, _yuzey, []) : null;
        var pencere = new AraziKarsilastirmaWindow(ilk);
        Kst.Acoustics.App.Views.WindowNavigation.CopyPlacement(this, pencere);
        await pencere.ShowDialog(this);
    }

    private void Geri_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || !BekleyenFormYok()) return;
        if (_geriDon == null) { Close(); return; }
        var win = _geriDon(); Kst.Acoustics.App.Views.WindowNavigation.CopyPlacement(this, win);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = win;
        win.Show(); Close();
    }
}
