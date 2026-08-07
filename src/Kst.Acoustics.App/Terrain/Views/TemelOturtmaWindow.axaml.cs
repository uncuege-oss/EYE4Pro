using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

/// M61 — Bina Temeli Kota Oturtma penceresi. Salt-okunur arazi (.e4arazi) + bina (.e4bina)
/// yükler, ayak izini araziye oturtur, kotları ve 3B temel bloğunu gösterir.
public partial class TemelOturtmaWindow : Window
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly Func<Window>? _geriDon;
    private AraziTuketim? _arazi;
    private readonly List<BinaAyakIzi> _ayaklar = new();
    private TemelOturtmaModeli? _model;
    private BlokOturma? _seciliBlok;

    public TemelOturtmaWindow() : this(null) { }

    public TemelOturtmaWindow(Func<Window>? geriDon)
    {
        InitializeComponent();
        _geriDon = geriDon;
    }

    private async void AraziYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Arazi Seç (.e4arazi.json)", "EYE4PRO Arazi", "*.e4arazi.json");
        if (path is null) return;
        try
        {
            _arazi = await Task.Run(() => AraziTuketimServisi.Yukle(path));
            Onizleme3D.Surfaces = Arazi3DGorunumServisi.YuzeyleriOlustur(_arazi.Yuzey);
            string aralik = _arazi.KotAraligi() is { } r ? $"{r.Min:0.##}–{r.Max:0.##} m" : "—";
            DurumText.Text = $"Arazi yüklendi (rev {_arazi.Revizyon}) · kot {aralik} · {_arazi.UcgenSayisi} üçgen.";
        }
        catch (Exception ex) { DurumText.Text = "Arazi yüklenemedi: " + ex.Message; }
    }

    private async void BinaEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Bina Seç (.e4bina.json)", "EYE4PRO Bina Geometrisi", "*.e4bina.json");
        if (path is null) return;
        try
        {
            var ayak = await Task.Run(() =>
            {
                var geo = BinaGeometriServisi.Ac(path);
                return BinaAyakIziAdaptoru.Donustur(geo);
            });
            _ayaklar.Add(ayak);
            DurumText.Text = $"Bina eklendi: '{ayak.BlokId}' ({ayak.ZeminUstuKatSayisi} üst, " +
                             $"{ayak.BodrumKatSayisi} bodrum). Toplam {_ayaklar.Count} bina.";
        }
        catch (Exception ex) { DurumText.Text = "Bina yüklenemedi: " + ex.Message; }
    }

    private async void MimariEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await DosyaSec("Mimari Proje Seç (.e4mimari.json)", "EYE4PRO Mimari", "*.e4mimari.json");
        if (path is null) return;
        try
        {
            var ayak = await Task.Run(() =>
            {
                var proje = MimariProjeServisi.Ac(path);
                var plan = proje.PlanTaslagi;
                var model = plan?.Model3D ?? Model3DUretimServisi.Uret(plan);
                return BinaAyakIziAdaptoru.Donustur(model);
            });
            _ayaklar.Add(ayak);
            DurumText.Text = $"Mimari bina eklendi: '{ayak.BlokId}' ({ayak.ZeminUstuKatSayisi} üst, " +
                             $"{ayak.BodrumKatSayisi} bodrum, ayak izi {ayak.Poligon.Count} köşe). Toplam {_ayaklar.Count} bina.";
        }
        catch (Exception ex) { DurumText.Text = "Mimari yüklenemedi: " + ex.Message; }
    }

    private void BinalariTemizle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ayaklar.Clear();
        _model = null;
        _seciliBlok = null;
        BlokSecici.ItemsSource = null;
        if (_arazi is not null)
            Onizleme3D.Surfaces = Arazi3DGorunumServisi.YuzeyleriOlustur(_arazi.Yuzey);
        SonucText.Text = "Binalar temizlendi.";
        DurumText.Text = "Bina listesi boş.";
    }

    private void Oturt_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_arazi is null) { DurumText.Text = "Önce arazi (.e4arazi) yükleyin."; return; }
        if (_ayaklar.Count == 0) { DurumText.Text = "Önce en az bir bina (.e4bina) ekleyin."; return; }

        double? sifirOverride = null;
        if (!string.IsNullOrWhiteSpace(SifirKotBox.Text))
        {
            if (Sayi(SifirKotBox.Text, out double s)) sifirOverride = s;
            else { DurumText.Text = "±0.00 değeri geçersiz."; return; }
        }

        // ±0.00 override tüm bloklara uygulanırsa kademe etkisi kaybolur; override yalnız
        // tek bina varken anlamlı. Çok blokta her blok kendi zeminine oturur (override atlanır).
        if (sifirOverride.HasValue && _ayaklar.Count == 1)
            _model = TekBlokOverride(_arazi, _ayaklar[0], sifirOverride.Value);
        else
            _model = new TemelOturtmaServisi().Oturt(_arazi, _ayaklar);

        // Blok seçiciyi doldur.
        BlokSecici.ItemsSource = _model.Bloklar.Select(b => b.BlokId).ToList();
        BlokSecici.SelectedIndex = _model.Bloklar.Count > 0 ? 0 : -1;

        Yenile();
        DurumText.Text = $"{_model.Bloklar.Count} blok oturtuldu (kademe sırasıyla).";
    }

    private void Kesit_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_arazi is null || _model is null || _model.Bloklar.Count == 0)
        {
            DurumText.Text = "Önce arazi + bina yükleyip Oturt'a basın.";
            return;
        }
        var kesit = KesitServisi.Olustur(_arazi, _model, _ayaklar);
        new KesitWindow(kesit) { Title = $"Kotlu Kesit — {_model.AraziAdi}" }.Show();
    }

    private static TemelOturtmaModeli TekBlokOverride(AraziTuketim arazi, BinaAyakIzi ayak, double sifir)
    {
        var model = new TemelOturtmaModeli { AraziAdi = arazi.AraziAdi, AraziRevizyon = arazi.Revizyon };
        model.Bloklar.Add(new TemelOturtmaServisi().OturtBlok(arazi, ayak, sifir));
        return model;
    }

    private void BlokSecici_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model is null) return;
        int i = BlokSecici.SelectedIndex;
        _seciliBlok = (i >= 0 && i < _model.Bloklar.Count) ? _model.Bloklar[i] : null;
        if (_seciliBlok is not null) KotKutulariniDoldur(_seciliBlok);
    }

    /// F3 — düzenlenen kotları (çıkış / yangın merdiveni / bodrum taban) uygular,
    /// kazı derinliğini ve uyarıları yeniden hesaplar, 3B ve raporu günceller.
    private void KotlariUygula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var b = _seciliBlok;
        if (b is null || b.Kotlar.Count == 0)
        {
            DurumText.Text = "Önce Oturt'a basıp bir blok seçin.";
            return;
        }

        if (Sayi(CikisBox.Text, out double cikis)) KotAyarla(b, KotTipi.Cikis, cikis);
        if (Sayi(YanginBox.Text, out double yangin)) KotAyarla(b, KotTipi.YanginMerdiveni, yangin);
        if (Sayi(BodrumBox.Text, out double bodrum))
        {
            KotAyarla(b, KotTipi.Bodrum, bodrum);
            b.BodrumTabanKotuM = bodrum;
            b.KaziDerinligiM = Math.Max(0.0, b.OrtalamaZeminKotuM - bodrum);
        }

        // Düzenleme uyarılarını yeniden değerlendir (önceki düzenleme uyarılarını temizle).
        b.Uyarilar.RemoveAll(u => u.StartsWith("Düzenleme:"));
        foreach (var k in b.Kotlar)
        {
            if ((k.Tip == KotTipi.Cikis || k.Tip == KotTipi.YanginMerdiveni) && k.KotM > b.CatiKotuM)
                b.Uyarilar.Add($"Düzenleme: {k.Tip} kotu çatının üstünde.");
            if (k.Tip == KotTipi.Bodrum && k.KotM > b.OrtalamaZeminKotuM)
                b.Uyarilar.Add("Düzenleme: bodrum taban tabii zeminin üstünde; kazı oluşmaz.");
        }

        Yenile();
        DurumText.Text = $"'{b.BlokId}' kotları güncellendi · kazı {b.KaziDerinligiM:0.##} m.";
    }

    private static void KotAyarla(BlokOturma b, KotTipi tip, double kotM)
    {
        var mevcut = b.Kotlar.FirstOrDefault(k => k.Tip == tip);
        if (mevcut is not null) mevcut.KotM = kotM;
        else b.Kotlar.Add(new KotTanimi(tip, kotM));
    }

    private void KotKutulariniDoldur(BlokOturma b)
    {
        double? Kot(KotTipi t) => b.Kotlar.FirstOrDefault(k => k.Tip == t)?.KotM;
        GirisBox.Text = b.SifirKotM.ToString("0.##", Tr);
        CikisBox.Text = (Kot(KotTipi.Cikis) ?? b.SifirKotM).ToString("0.##", Tr);
        YanginBox.Text = (Kot(KotTipi.YanginMerdiveni) ?? b.SifirKotM).ToString("0.##", Tr);
        BodrumBox.Text = (Kot(KotTipi.Bodrum) ?? b.BodrumTabanKotuM).ToString("0.##", Tr);
    }

    /// 3B önizlemeyi (arazi TIN + tüm temel kutuları) ve rapor metnini günceller.
    private void Yenile()
    {
        if (_arazi is null || _model is null) return;

        var yuzeyler = Arazi3DGorunumServisi.YuzeyleriOlustur(_arazi.Yuzey);
        // _ayaklar[i] ↔ _model.Bloklar[i] (aynı sıra). Her bloğa ayrı id aralığı.
        for (int i = 0; i < _model.Bloklar.Count && i < _ayaklar.Count; i++)
        {
            var b = _model.Bloklar[i];
            if (b.Kotlar.Count == 0) continue;
            yuzeyler.AddRange(Arazi3DGorunumServisi.BlokKutusuYuzeyleri(
                _ayaklar[i].Poligon, b.BodrumTabanKotuM, b.CatiKotuM, b.BlokId, 20000 + i * 1000));
        }
        Onizleme3D.Surfaces = yuzeyler;

        SonucText.Text = RaporMetni(_model);
    }

    private static string RaporMetni(TemelOturtmaModeli model)
    {
        string M(double v) => v.ToString("0.##", Tr) + " m";
        var sb = new StringBuilder();
        sb.AppendLine($"ARAZİ: {model.AraziAdi} (rev {model.AraziRevizyon})");
        sb.AppendLine($"{model.Bloklar.Count} blok, kademe sırasıyla:");
        sb.AppendLine();

        foreach (var b in model.Bloklar.OrderBy(x => x.KademeNo))
        {
            sb.AppendLine($"── Kademe {b.KademeNo}: {b.BlokId} ──");
            sb.AppendLine($"  Tabii zemin: {M(b.OrtalamaZeminKotuM)}  ±0.00: {M(b.SifirKotM)}");
            sb.AppendLine($"  Bodrum taban: {M(b.BodrumTabanKotuM)}  Çatı: {M(b.CatiKotuM)}");
            sb.AppendLine($"  Kazı derinliği: {M(b.KaziDerinligiM)}");
            foreach (var k in b.Kotlar)
                sb.AppendLine($"    {k.Tip}: {M(k.KotM)}");
            foreach (var u in b.Uyarilar)
                sb.AppendLine($"    ⚠ {u}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_geriDon is null) { Close(); return; }
        OpenWindowOnSameScreen(_geriDon());
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private async Task<string?> DosyaSec(string baslik, string tipAd, string desen)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = baslik,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(tipAd) { Patterns = [desen] }]
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private static bool Sayi(string? metin, out double deger)
    {
        deger = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;
        return double.TryParse(metin.Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out deger);
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
        else { win.Position = this.Position; }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        Close();
    }
}
