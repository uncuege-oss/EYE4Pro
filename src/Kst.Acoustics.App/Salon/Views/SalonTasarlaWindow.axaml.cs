using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Salon.ViewModels;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonTasarlaWindow : Window
{
    private readonly SalonTasarlaViewModel _vm = new();
    private bool _kapanisOnaylandi;

    // Undo/redo: değişiklik ÖNCESİ alınan JSON anlık görüntüleri (M12 snapshot deseni).
    private readonly System.Collections.Generic.List<string> _geriYigini = new();
    private readonly System.Collections.Generic.List<string> _ileriYigini = new();
    private const int UndoLimiti = 50;

    // Geri butonunun döneceği pencere fabrikası — her launcher kendi tipini iletir.
    private readonly Func<Window>? _geriPencere;

    // Parameterless ctor: Avalonia AXAML loader için zorunlu.
    public SalonTasarlaWindow() : this(null) { }

    public SalonTasarlaWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = _vm;
        _geriPencere = geriPencere;
        Opened += OnOpened;
        Closing += OnWindowClosing;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        SyncPanel();

        if (CizimCanvas is { } c)
        {
            c.OnDurum = m => _vm.DurumMesaji = m;
            c.OnSecimDegisti = _ => { };
            c.OnDegisti = () => _vm.IsDirty = true;
            c.OnDegisecek = SnapshotAl;
            c.OnAciklikYerlestir = AciklikYerlestir;
            BaglaCanvas();
            AracIsaretle(AracSec);
            c.Arac = SalonAraci.Sec;
        }

        _vm.PropertyChanged += (_, ev) =>
        {
            switch (ev.PropertyName)
            {
                case nameof(SalonTasarlaViewModel.SeciliKatman):
                    if (CizimCanvas is { } cc) cc.AktifKatman = _vm.SeciliKatman;
                    SyncKatmanDuzenle();
                    break;
                case nameof(SalonTasarlaViewModel.SeciliSeyirci):
                    SyncSeyirciAlanlari();
                    break;
                case nameof(SalonTasarlaViewModel.SeciliBalkon):
                    SyncBalkonAlanlari();
                    break;
                case nameof(SalonTasarlaViewModel.SeciliAciklik):
                    SyncAciklikAlanlari();
                    break;
            }
        };
        SyncKatmanDuzenle();
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    /// Model değişmeden hemen önce çağrılır: mevcut durumu undo yığınına atar.
    private void SnapshotAl()
    {
        _geriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        if (_geriYigini.Count > UndoLimiti) _geriYigini.RemoveAt(0);
        _ileriYigini.Clear();
        _vm.IsDirty = true;
    }

    private void UndoSifirla()
    {
        _geriYigini.Clear();
        _ileriYigini.Clear();
    }

    private void GeriAl()
    {
        if (_geriYigini.Count == 0) { _vm.DurumMesaji = "Geri alınacak işlem yok."; return; }
        _ileriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        var json = _geriYigini[^1];
        _geriYigini.RemoveAt(_geriYigini.Count - 1);
        SalonuGeriYukle(json);
        _vm.DurumMesaji = "Geri alındı (Ctrl+Z).";
    }

    private void Yinele()
    {
        if (_ileriYigini.Count == 0) { _vm.DurumMesaji = "Yinelenecek işlem yok."; return; }
        _geriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        var json = _ileriYigini[^1];
        _ileriYigini.RemoveAt(_ileriYigini.Count - 1);
        SalonuGeriYukle(json);
        _vm.DurumMesaji = "Yinelendi (Ctrl+Y).";
    }

    private void SalonuGeriYukle(string json)
    {
        _vm.Salon = SalonGeometriServisi.KopyadanYukle(json);
        _vm.IsDirty = true;
        _vm.BasligiYenile();
        SyncPanel();
        BaglaCanvas();
        CizimCanvas?.InvalidateVisual();
    }

    // ── Katman yönetimi ───────────────────────────────────────────────────────

    private void SyncKatmanDuzenle()
    {
        var k = _vm.SeciliKatman;
        if (KatmanAdText is { } a) a.Text = k?.Ad ?? "";
        if (KatmanRenkText is { } r) r.Text = k?.RenkHex ?? "";
        if (KatmanTipCombo is { } c) c.SelectedIndex = k is not null ? (int)k.Tip : 0;
    }

    private void OgeyiKatmanaAta_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        CizimCanvas?.SeciliyeKatmanAta(k.Id);
    }

    private void KatmanGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        SnapshotAl();

        if (!string.IsNullOrWhiteSpace(KatmanAdText?.Text))
            k.Ad = KatmanAdText.Text.Trim();

        var hex = KatmanRenkText?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try { _ = Avalonia.Media.Color.Parse(hex); k.RenkHex = hex; }
            catch { _vm.DurumMesaji = "Geçersiz renk (#RRGGBB bekleniyor)."; return; }
        }

        k.Tip = (SalonKatmanTipi)(KatmanTipCombo?.SelectedIndex ?? 0);
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"Katman güncellendi: {k.Ad}";
    }

    private void KatmanSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        if (_vm.Katmanlar.Count <= 1) { _vm.DurumMesaji = "En az bir katman kalmalı."; return; }
        SnapshotAl();

        var hedef = _vm.Katmanlar.First(x => !ReferenceEquals(x, k));
        int tasinan = 0;
        foreach (var oge in _vm.Salon.CizimOgeleri)
            if (oge.KatmanId == k.Id) { oge.KatmanId = hedef.Id; tasinan++; }

        _vm.Katmanlar.Remove(k);
        _vm.SeciliKatman = hedef;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = tasinan > 0
            ? $"Katman silindi; {tasinan} öğe '{hedef.Ad}' katmanına taşındı."
            : "Katman silindi.";
    }

    /// Canvas'ı güncel salon + aktif katmanla bağlar (Yeni/Aç sonrası da çağrılır).
    private void BaglaCanvas()
    {
        if (CizimCanvas is not { } c) return;
        c.Salon = _vm.Salon;
        c.AktifKatman = _vm.SeciliKatman;
        c.InvalidateVisual();
    }

    private void SyncPanel()
    {
        if (SalonAdiText is { } t) t.Text = _vm.Salon.SalonAdi;
        if (YukseklikText is { } y) y.Text = _vm.Salon.SalonYuksekligiM.ToString("F2");
        if (FasetText is { } f) f.Text = _vm.Salon.FasetKenarSayisi.ToString();
        SyncSahnePanel();
    }

    private void Faset_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (int.TryParse(FasetText?.Text, out int v))
        {
            v = Math.Clamp(v, 12, 128);
            if (v != _vm.Salon.FasetKenarSayisi)
            {
                SnapshotAl();
                _vm.Salon.FasetKenarSayisi = v;
                CizimCanvas?.InvalidateVisual();
                _vm.DurumMesaji = $"Faset kenar sayısı: {v} (daire/oval 3D-DXF çözünürlüğü).";
            }
        }
        if (FasetText is { } tb) tb.Text = _vm.Salon.FasetKenarSayisi.ToString();
    }

    // ── Sahne ─────────────────────────────────────────────────────────────────

    private void SyncSahnePanel()
    {
        var s = _vm.Salon.Sahne;
        if (SahneTipCombo is { } tc) tc.SelectedIndex = s is not null ? (int)s.Tip : 0;
        if (SahneKonumCombo is { } kc) kc.SelectedIndex = s is not null ? (int)s.Konum : 1;
        if (SahneGenislikText is { } g) g.Text = s is not null ? s.GenislikM.ToString("F2") : "";
        if (SahneDerinlikText is { } d) d.Text = s is not null ? s.DerinlikM.ToString("F2") : "";
        if (SahneYukseklikText is { } yk) yk.Text = s is not null ? s.YukseklikM.ToString("F2") : "";
        if (SahneMerkezXText is { } mx) mx.Text = s is not null ? s.MerkezX.ToString("F2") : "";
        if (SahneMerkezYText is { } my) my.Text = s is not null ? s.MerkezY.ToString("F2") : "";
        if (SahneKotText is { } kt) kt.Text = s is not null ? s.TabanKotuM.ToString("F2") : "";
        SyncCukurPanel();
    }

    private void SyncCukurPanel()
    {
        var c = _vm.Salon.OrkestraCukuru;
        if (CukurGenislikText is { } g) g.Text = c is not null ? c.GenislikM.ToString("F2") : "";
        if (CukurDerinlikText is { } d) d.Text = c is not null ? c.DerinlikM.ToString("F2") : "";
        if (CukurDusumText is { } cd) cd.Text = c is not null ? c.CukurDerinligiM.ToString("F2") : "";
        if (CukurMerkezXText is { } mx) mx.Text = c is not null ? c.MerkezX.ToString("F2") : "";
        if (CukurMerkezYText is { } my) my.Text = c is not null ? c.MerkezY.ToString("F2") : "";
    }

    private void CukurUygula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapshotAl();
        var c = _vm.Salon.OrkestraCukuru ?? new OrkestraCukuru();
        c.GenislikM = SahnePozitif(CukurGenislikText, c.GenislikM, 12.0);
        c.DerinlikM = SahnePozitif(CukurDerinlikText, c.DerinlikM, 3.0);
        c.CukurDerinligiM = SahnePozitif(CukurDusumText, c.CukurDerinligiM, 2.5);

        // Varsayılan yerleşim: sahnenin hemen önü (seyirci tarafı = +Y kenarı), yoksa orijin.
        double dcx = 0, dcy = 0;
        if (_vm.Salon.Sahne is { } sahne)
        {
            dcx = sahne.MerkezX;
            dcy = sahne.MerkezY + sahne.DerinlikM / 2 + c.DerinlikM / 2;
        }
        c.MerkezX = SahneEksen(CukurMerkezXText, _vm.Salon.OrkestraCukuru?.MerkezX ?? dcx);
        c.MerkezY = SahneEksen(CukurMerkezYText, _vm.Salon.OrkestraCukuru?.MerkezY ?? dcy);

        _vm.Salon.OrkestraCukuru = c;
        SyncCukurPanel();
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"Orkestra çukuru: {c.GenislikM:F1}×{c.DerinlikM:F1} m · −{c.CukurDerinligiM:F1} m";
    }

    private void CukurKaldir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Salon.OrkestraCukuru is null) return;
        SnapshotAl();
        _vm.Salon.OrkestraCukuru = null;
        SyncCukurPanel();
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = "Orkestra çukuru kaldırıldı.";
    }

    /// Seçili seyirci alanının değerlerini giriş kutularına doldurur (Güncelle akışı).
    private void SyncSeyirciAlanlari()
    {
        if (_vm.SeciliSeyirci is not { } s) return;
        if (SeyirciGenislikText is { } g) g.Text = s.GenislikM.ToString("F2");
        if (SeyirciDerinlikText is { } d) d.Text = s.DerinlikM.ToString("F2");
        if (SeyirciKademeText is { } k) k.Text = s.KademeSayisi.ToString();
        if (SeyirciYukText is { } y) y.Text = s.KademeYuksekligiM.ToString("F2");
        if (SeyirciKotText is { } kt) kt.Text = s.TabanKotuM.ToString("F2");
        if (SeyirciMerkezXText is { } mx) mx.Text = s.MerkezX.ToString("F2");
        if (SeyirciMerkezYText is { } my) my.Text = s.MerkezY.ToString("F2");
        if (SeyirciTersCheck is { } t) t.IsChecked = s.TersYon;
    }

    /// Seçili balkonun değerlerini giriş kutularına doldurur (Güncelle akışı).
    private void SyncBalkonAlanlari()
    {
        if (_vm.SeciliBalkon is not { } b) return;
        if (BalkonTipCombo is { } tc) tc.SelectedIndex = (int)b.Tip;
        if (BalkonGenislikText is { } g) g.Text = b.GenislikM.ToString("F2");
        if (BalkonDerinlikText is { } d) d.Text = b.DerinlikM.ToString("F2");
        if (BalkonKotText is { } k) k.Text = b.TabanKotuM.ToString("F2");
        if (BalkonKalinlikText is { } kl) kl.Text = b.KalinlikM.ToString("F2");
        if (BalkonParapetText is { } p) p.Text = b.ParapetM.ToString("F2");
        if (BalkonMerkezXText is { } mx) mx.Text = b.MerkezX.ToString("F2");
        if (BalkonMerkezYText is { } my) my.Text = b.MerkezY.ToString("F2");
    }

    private void SahneUygula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapshotAl();
        var s = _vm.Salon.Sahne ?? new SahneGeometri();
        s.Tip = (SahneTipi)(SahneTipCombo?.SelectedIndex ?? 0);
        s.Konum = (SahneKonum)(SahneKonumCombo?.SelectedIndex ?? 1);
        s.GenislikM = SahnePozitif(SahneGenislikText, s.GenislikM, 12.0);
        s.DerinlikM = SahnePozitif(SahneDerinlikText, s.DerinlikM, 8.0);
        s.YukseklikM = SahnePozitif(SahneYukseklikText, s.YukseklikM, 1.0);
        s.TabanKotuM = SahneEksen(SahneKotText, s.TabanKotuM);

        var (dcx, dcy) = VarsayilanSahneMerkezi(s);
        s.MerkezX = SahneEksen(SahneMerkezXText, dcx);
        s.MerkezY = SahneEksen(SahneMerkezYText, dcy);

        _vm.Salon.Sahne = s;
        SyncSahnePanel();
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"Sahne: {SahneTipAd(s.Tip)} · {s.GenislikM:F1}×{s.DerinlikM:F1} m · yük {s.YukseklikM:F1} m";
    }

    private void SahneKaldir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Salon.Sahne is null) return;
        SnapshotAl();
        _vm.Salon.Sahne = null;
        SyncSahnePanel();
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = "Sahne kaldırıldı.";
    }

    private static string SahneTipAd(SahneTipi t) => t switch
    {
        SahneTipi.Dairesel => "Dairesel",
        SahneTipi.Oval => "Oval",
        _ => "Dikdörtgen"
    };

    private static double SahnePozitif(TextBox? tb, double mevcut, double varsayilan)
    {
        if (double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0)
            return v;
        return mevcut > 0 ? mevcut : varsayilan;
    }

    private static double SahneEksen(TextBox? tb, double varsayilan)
        => double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? v : varsayilan;

    // ── Seyirci alanı ─────────────────────────────────────────────────────────

    private void SeyirciEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapshotAl();
        var sek = new SeyirciSekciyonu
        {
            Ad = $"Seyirci {_vm.SeyirciSekciyonlari.Count + 1}",
            GenislikM = SahnePozitif(SeyirciGenislikText, 0, 16),
            DerinlikM = SahnePozitif(SeyirciDerinlikText, 0, 12),
            KademeSayisi = (int)System.Math.Round(SahnePozitif(SeyirciKademeText, 0, 6)),
            KademeYuksekligiM = SahnePozitif(SeyirciYukText, 0, 0.45),
            TabanKotuM = SahneEksen(SeyirciKotText, 0),
            TersYon = SeyirciTersCheck?.IsChecked == true
        };
        if (sek.KademeSayisi < 1) sek.KademeSayisi = 1;

        var (cx, cy) = VarsayilanSeyirciMerkezi(sek);
        sek.MerkezX = SahneEksen(SeyirciMerkezXText, cx);
        sek.MerkezY = SahneEksen(SeyirciMerkezYText, cy);

        _vm.SeyirciSekciyonlari.Add(sek);
        _vm.SeciliSeyirci = sek;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = SeyirciOzeti(sek, "eklendi");
    }

    /// Seçili seyirci alanını giriş kutularındaki değerlerle günceller.
    private void SeyirciGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliSeyirci is not { } sek) { _vm.DurumMesaji = "Önce listeden bir seyirci alanı seçin."; return; }
        SnapshotAl();

        sek.GenislikM = SahnePozitif(SeyirciGenislikText, sek.GenislikM, 16);
        sek.DerinlikM = SahnePozitif(SeyirciDerinlikText, sek.DerinlikM, 12);
        sek.KademeSayisi = System.Math.Max(1,
            (int)System.Math.Round(SahnePozitif(SeyirciKademeText, sek.KademeSayisi, 6)));
        sek.KademeYuksekligiM = SahnePozitif(SeyirciYukText, sek.KademeYuksekligiM, 0.45);
        sek.TabanKotuM = SahneEksen(SeyirciKotText, sek.TabanKotuM);
        sek.MerkezX = SahneEksen(SeyirciMerkezXText, sek.MerkezX);
        sek.MerkezY = SahneEksen(SeyirciMerkezYText, sek.MerkezY);
        sek.TersYon = SeyirciTersCheck?.IsChecked == true;

        ListeyiTazele(_vm.SeyirciSekciyonlari, sek);
        _vm.SeciliSeyirci = sek;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = SeyirciOzeti(sek, "güncellendi");
    }

    private string SeyirciOzeti(SeyirciSekciyonu sek, string eylem)
    {
        double egim = sek.DerinlikM > 0 ? sek.KademeYuksekligiM * sek.KademeSayisi / sek.DerinlikM * 100 : 0;
        int koltuk = Kst.Acoustics.Core.Salon.Services.SalonSeyirciServisi.KoltukKapasitesi(sek);
        return $"{sek.Ad} {eylem} · {sek.KademeSayisi} kademe · ~%{egim:F0} eğim · ~{koltuk} koltuk";
    }

    /// ObservableCollection öğesini yerinde tazeler (INPC olmayan öğe metni için).
    private static void ListeyiTazele<T>(System.Collections.ObjectModel.ObservableCollection<T> liste, T oge)
    {
        int i = liste.IndexOf(oge);
        if (i < 0) return;
        liste.RemoveAt(i);
        liste.Insert(i, oge);
    }

    private void SeyirciSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliSeyirci is not { } sek) { _vm.DurumMesaji = "Önce listeden bir seyirci alanı seçin."; return; }
        SnapshotAl();
        _vm.SeyirciSekciyonlari.Remove(sek);
        _vm.SeciliSeyirci = null;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = "Seyirci alanı silindi.";
    }

    /// Seyirci alanı için varsayılan merkez: sahne varsa onun arkasına, yoksa çizim ortasına.
    private (double X, double Y) VarsayilanSeyirciMerkezi(SeyirciSekciyonu sek)
    {
        if (_vm.Salon.Sahne is { } sahne)
            return (sahne.MerkezX, sahne.MerkezY + sahne.DerinlikM / 2 + sek.DerinlikM / 2 + 1.0);

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var o in _vm.Salon.CizimOgeleri)
            foreach (var (a, b) in o.Segmentler())
            {
                minX = System.Math.Min(minX, System.Math.Min(a.X, b.X));
                maxX = System.Math.Max(maxX, System.Math.Max(a.X, b.X));
                minY = System.Math.Min(minY, System.Math.Min(a.Y, b.Y));
                maxY = System.Math.Max(maxY, System.Math.Max(a.Y, b.Y));
                any = true;
            }
        return any ? ((minX + maxX) / 2, (minY + maxY) / 2) : (0, 0);
    }

    // ── Balkon / Loca ─────────────────────────────────────────────────────────

    private void BalkonEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapshotAl();
        var tip = (BalkonTipi)(BalkonTipCombo?.SelectedIndex ?? 0);
        var b = new BalkonGeometri
        {
            Tip = tip,
            Ad = $"{(tip == BalkonTipi.Loca ? "Loca" : "Balkon")} {_vm.Balkonlar.Count + 1}",
            GenislikM = SahnePozitif(BalkonGenislikText, 0, tip == BalkonTipi.Loca ? 4 : 16),
            DerinlikM = SahnePozitif(BalkonDerinlikText, 0, tip == BalkonTipi.Loca ? 3 : 5),
            TabanKotuM = SahneEksen(BalkonKotText, 5.0),
            KalinlikM = SahnePozitif(BalkonKalinlikText, 0, 0.4),
            ParapetM = SahneEksen(BalkonParapetText, 1.0)
        };

        var (cx, cy) = VarsayilanBalkonMerkezi(b);
        b.MerkezX = SahneEksen(BalkonMerkezXText, cx);
        b.MerkezY = SahneEksen(BalkonMerkezYText, cy);

        _vm.Balkonlar.Add(b);
        _vm.SeciliBalkon = b;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"{b.Ad} eklendi · kot {b.TabanKotuM:F1} m · parapet {b.ParapetM:F1} m";
    }

    /// Seçili balkon/locayı giriş kutularındaki değerlerle günceller.
    private void BalkonGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliBalkon is not { } b) { _vm.DurumMesaji = "Önce listeden bir balkon/loca seçin."; return; }
        SnapshotAl();

        b.Tip = (BalkonTipi)(BalkonTipCombo?.SelectedIndex ?? (int)b.Tip);
        b.GenislikM = SahnePozitif(BalkonGenislikText, b.GenislikM, 16);
        b.DerinlikM = SahnePozitif(BalkonDerinlikText, b.DerinlikM, 5);
        b.TabanKotuM = SahneEksen(BalkonKotText, b.TabanKotuM);
        b.KalinlikM = SahnePozitif(BalkonKalinlikText, b.KalinlikM, 0.4);
        b.ParapetM = SahneEksen(BalkonParapetText, b.ParapetM);
        b.MerkezX = SahneEksen(BalkonMerkezXText, b.MerkezX);
        b.MerkezY = SahneEksen(BalkonMerkezYText, b.MerkezY);

        ListeyiTazele(_vm.Balkonlar, b);
        _vm.SeciliBalkon = b;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"{b.Ad} güncellendi · kot {b.TabanKotuM:F1} m · parapet {b.ParapetM:F1} m";
    }

    private void BalkonSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliBalkon is not { } b) { _vm.DurumMesaji = "Önce listeden bir balkon/loca seçin."; return; }
        SnapshotAl();
        _vm.Balkonlar.Remove(b);
        _vm.SeciliBalkon = null;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = "Balkon/loca silindi.";
    }

    /// Balkon için varsayılan merkez: seyirci varsa onun arkasına, yoksa çizim ortasına.
    private (double X, double Y) VarsayilanBalkonMerkezi(BalkonGeometri b)
    {
        if (_vm.Salon.SeyirciSekciyonlari.Count > 0)
        {
            var s = _vm.Salon.SeyirciSekciyonlari[^1];
            return (s.MerkezX, s.MerkezY + s.DerinlikM / 2 + b.DerinlikM / 2);
        }
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var o in _vm.Salon.CizimOgeleri)
            foreach (var (a, bb) in o.Segmentler())
            {
                minX = System.Math.Min(minX, System.Math.Min(a.X, bb.X));
                maxX = System.Math.Max(maxX, System.Math.Max(a.X, bb.X));
                minY = System.Math.Min(minY, System.Math.Min(a.Y, bb.Y));
                maxY = System.Math.Max(maxY, System.Math.Max(a.Y, bb.Y));
                any = true;
            }
        return any ? ((minX + maxX) / 2, (maxY + b.DerinlikM / 2)) : (0, 0);
    }

    /// Çizilen içeriğe göre sahne merkezini önerir (Salon önü = altta, Salon içi = ortada).
    private (double X, double Y) VarsayilanSahneMerkezi(SahneGeometri s)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var o in _vm.Salon.CizimOgeleri)
            foreach (var (a, b) in o.Segmentler())
            {
                minX = System.Math.Min(minX, System.Math.Min(a.X, b.X));
                maxX = System.Math.Max(maxX, System.Math.Max(a.X, b.X));
                minY = System.Math.Min(minY, System.Math.Min(a.Y, b.Y));
                maxY = System.Math.Max(maxY, System.Math.Max(a.Y, b.Y));
                any = true;
            }
        if (!any)
            return (0, s.Konum == SahneKonum.SalonOnu ? -(s.DerinlikM / 2 + 1) : 0);

        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        return s.Konum == SahneKonum.SalonOnu
            ? (cx, minY - s.DerinlikM / 2 - 1.0)
            : (cx, cy);
    }

    // ── Açıklıklar (kapı/pencere) ─────────────────────────────────────────────

    /// Canvas 🚪 aracı: duvar üzerine tıklanan noktada paneldeki ölçülerle açıklık oluşturur.
    private void AciklikYerlestir(Kst.Acoustics.Core.Common.Geometry.Point2D nokta)
    {
        SnapshotAl();
        var tip = (AciklikTipi)(AciklikTipCombo?.SelectedIndex ?? 0);
        var ac = new DuvarAcikligi
        {
            Tip = tip,
            Ad = $"{(tip == AciklikTipi.Pencere ? "Pencere" : "Kapı")} {_vm.Acikliklar.Count + 1}",
            GenislikM = SahnePozitif(AciklikGenText, 0, tip == AciklikTipi.Pencere ? 1.5 : 1.0),
            YukseklikM = SahnePozitif(AciklikYukText, 0, tip == AciklikTipi.Pencere ? 1.2 : 2.1),
            AltKotM = System.Math.Max(0, SahneEksen(AciklikKotText, tip == AciklikTipi.Pencere ? 1.0 : 0)),
            MerkezX = nokta.X,
            MerkezY = nokta.Y
        };
        _vm.Acikliklar.Add(ac);
        _vm.SeciliAciklik = ac;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"{ac.Ad} yerleştirildi ({ac.GenislikM:F1}×{ac.YukseklikM:F1} m @ +{ac.AltKotM:F1} m).";
    }

    private void SyncAciklikAlanlari()
    {
        if (_vm.SeciliAciklik is not { } ac) return;
        if (AciklikTipCombo is { } t) t.SelectedIndex = (int)ac.Tip;
        if (AciklikGenText is { } g) g.Text = ac.GenislikM.ToString("F2");
        if (AciklikYukText is { } y) y.Text = ac.YukseklikM.ToString("F2");
        if (AciklikKotText is { } k) k.Text = ac.AltKotM.ToString("F2");
        if (AciklikMerkezXText is { } mx) mx.Text = ac.MerkezX.ToString("F2");
        if (AciklikMerkezYText is { } my) my.Text = ac.MerkezY.ToString("F2");
    }

    private void AciklikGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliAciklik is not { } ac) { _vm.DurumMesaji = "Önce listeden bir açıklık seçin."; return; }
        SnapshotAl();

        ac.Tip = (AciklikTipi)(AciklikTipCombo?.SelectedIndex ?? (int)ac.Tip);
        ac.GenislikM = SahnePozitif(AciklikGenText, ac.GenislikM, 1.0);
        ac.YukseklikM = SahnePozitif(AciklikYukText, ac.YukseklikM, 2.1);
        ac.AltKotM = System.Math.Max(0, SahneEksen(AciklikKotText, ac.AltKotM));
        ac.MerkezX = SahneEksen(AciklikMerkezXText, ac.MerkezX);
        ac.MerkezY = SahneEksen(AciklikMerkezYText, ac.MerkezY);

        ListeyiTazele(_vm.Acikliklar, ac);
        _vm.SeciliAciklik = ac;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"{ac.Ad} güncellendi.";
    }

    private void AciklikSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliAciklik is not { } ac) { _vm.DurumMesaji = "Önce listeden bir açıklık seçin."; return; }
        SnapshotAl();
        _vm.Acikliklar.Remove(ac);
        _vm.SeciliAciklik = null;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = "Açıklık silindi.";
    }

    // ── Tavan profili ─────────────────────────────────────────────────────────

    private void TavanNoktaEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        double? y = SayiOku(TavanYText);
        double? kot = SayiOku(TavanKotText);
        if (y is null || kot is null || kot <= 0)
        {
            _vm.DurumMesaji = "Tavan noktası için Y (m) ve pozitif Kot (m) girin.";
            return;
        }

        SnapshotAl();
        // Aynı Y'de nokta varsa güncelle, yoksa sıralı konuma ekle.
        var mevcut = _vm.TavanProfili.FirstOrDefault(n => System.Math.Abs(n.YM - y.Value) < 1e-6);
        if (mevcut is not null)
        {
            mevcut.KotM = kot.Value;
            ListeyiTazele(_vm.TavanProfili, mevcut);
        }
        else
        {
            var nokta = new TavanKirilimNoktasi { YM = y.Value, KotM = kot.Value };
            int i = 0;
            while (i < _vm.TavanProfili.Count && _vm.TavanProfili[i].YM < nokta.YM) i++;
            _vm.TavanProfili.Insert(i, nokta);
        }

        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = _vm.TavanProfili.Count >= 2
            ? $"Tavan profili: {_vm.TavanProfili.Count} nokta (eğik/parçalı tavan aktif)."
            : "Tavan noktası eklendi — profil için en az 2 nokta gerekir.";
    }

    private void TavanNoktaSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliTavanNoktasi is not { } n) { _vm.DurumMesaji = "Önce listeden bir tavan noktası seçin."; return; }
        SnapshotAl();
        _vm.TavanProfili.Remove(n);
        _vm.SeciliTavanNoktasi = null;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = _vm.TavanProfili.Count >= 2
            ? "Tavan noktası silindi."
            : "Tavan noktası silindi — profil devre dışı (düz tavan).";
    }

    private void TavanDuz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.TavanProfili.Count == 0) return;
        SnapshotAl();
        _vm.TavanProfili.Clear();
        _vm.SeciliTavanNoktasi = null;
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = $"Düz tavana dönüldü ({_vm.Salon.SalonYuksekligiM:F1} m).";
    }

    private static double? SayiOku(TextBox? tb)
        => double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? v : null;

    // ── Araç seçimi (radio toggle) ────────────────────────────────────────────

    private void Arac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;
        if (!Enum.TryParse<SalonAraci>(tag, out var arac)) return;
        AracIsaretle(btn);
        if (CizimCanvas is { } c) c.Arac = arac;
    }

    private ToggleButton?[] AracButonlari => new[]
    {
        AracSec, AracCizgi, AracPolyline, AracDikdortgen, AracDaire, AracYay,
        AracTasi, AracAynala, AracDondur, AracOffset, AracTrim, AracUzat,
        AracSil, AracOlcum, AracAciklik
    };

    private void AracIsaretle(ToggleButton? aktif)
    {
        foreach (var b in AracButonlari)
            if (b is { }) b.IsChecked = ReferenceEquals(b, aktif);
    }

    /// Klavye kısayolu / programatik araç seçimi: butonu işaretler ve aracı etkinleştirir.
    private void AracSecKisayol(SalonAraci arac)
    {
        var btn = AracButonlari.FirstOrDefault(b => b?.Tag as string == arac.ToString());
        AracIsaretle(btn);
        if (CizimCanvas is { } c) c.Arac = arac;
    }

    private void Orto_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.OrthoToggle();
        if (OrtoBtn is { } b) b.IsChecked = aktif;
    }

    private void Snap_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.SnapToggle();
        if (SnapBtn is { } b) b.IsChecked = aktif;
    }

    private void Grid_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.GridToggle();
        if (GridBtn is { } b) b.IsChecked = aktif;
    }

    private void Polar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.PolarToggle();
        if (PolarBtn is { } b) b.IsChecked = aktif;
    }

    private async void DxfDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yuzeySayisi = Kst.Acoustics.Core.Salon.Services.Salon3DGorunumServisi.YuzeyleriOlustur(_vm.Salon).Count;
        if (yuzeySayisi == 0)
        {
            _vm.DurumMesaji = "DXF için önce 3D yüzey üretecek bir şeyler çizin.";
            return;
        }

        var ad = string.IsNullOrWhiteSpace(_vm.Salon.SalonAdi) ? "Salon" : _vm.Salon.SalonAdi;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salon DXF Dışa Aktar",
            DefaultExtension = "dxf",
            SuggestedFileName = $"{ad}.dxf",
            FileTypeChoices = [new FilePickerFileType("DXF (3DFACE)") { Patterns = ["*.dxf"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Kst.Acoustics.Core.Salon.Services.SalonDxfExportServisi.DisaAktar(_vm.Salon, path);
            _vm.DurumMesaji = $"DXF dışa aktarıldı: {Path.GetFileName(path)} ({yuzeySayisi} yüzey)";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF dışa aktarılamadı: {ex.Message}";
        }
    }

    private async void Kontrol_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.DurumMesaji = "Geometri kontrol ediliyor...";

        var satirlar = new System.Collections.Generic.List<string>();
        try
        {
            var uyarilar = SalonDogrulamaServisi.Dogrula(_vm.Salon);
            if (uyarilar.Count == 0)
                satirlar.Add("✔ Geometrik tutarlılık: sorun bulunamadı.");
            else
                satirlar.AddRange(uyarilar.Select(u => $"⚠ {u}"));

            // Kabuk varsa ışın tabanlı sızıntı testi (LeakTestService).
            bool kabukVar = !uyarilar.Any(u => u.Contains("Kapalı zemin poligonu yok"));
            if (kabukVar)
                satirlar.Add(await SalonDogrulamaServisi.KabukKapalilikRaporu(_vm.Salon));

            double hacim = SalonHacimServisi.Hesapla(_vm.Salon);
            if (hacim > 0) satirlar.Add($"Hacim (M01 Sabine V): {hacim:N0} m³");

            int koltuk = _vm.Salon.SeyirciSekciyonlari
                .Sum(s => Kst.Acoustics.Core.Salon.Services.SalonSeyirciServisi.KoltukKapasitesi(s));
            if (koltuk > 0)
            {
                satirlar.Add($"Tahmini kapasite: ~{koltuk} koltuk " +
                             $"({Kst.Acoustics.Core.Salon.Services.SalonSeyirciServisi.KoltukBasinaM2:F2} m²/koltuk brüt)" +
                             (hacim > 0 ? $" · {hacim / koltuk:F1} m³/koltuk" : ""));
            }

            if (Kst.Acoustics.Core.Salon.Services.SalonTavanServisi.ProfilliMi(_vm.Salon))
                satirlar.Add($"Tavan: profilli ({_vm.Salon.TavanProfili.Count} kırılım noktası, " +
                             $"kot {_vm.Salon.TavanProfili.Min(n => n.KotM):F1}–{_vm.Salon.TavanProfili.Max(n => n.KotM):F1} m).");
        }
        catch (Exception ex)
        {
            satirlar.Add($"Kontrol tamamlanamadı: {ex.Message}");
        }

        _vm.DurumMesaji = satirlar.Count > 0 ? satirlar[0] : "Kontrol tamamlandı.";
        await BilgiGoster("Geometri Kontrolü", string.Join("\n\n", satirlar));
    }

    private async Task BilgiGoster(string baslik, string mesaj)
    {
        var dialog = new Window
        {
            Title = baslik,
            Width = 560, SizeToContent = SizeToContent.Height,
            MaxHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var kapat = DialogButton("Kapat", "#0D2535", "#1A5070", Avalonia.Media.Brush.Parse("#C8E4F4"), 90);
        kapat.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new ScrollViewer
                {
                    MaxHeight = 380,
                    Content = new TextBlock
                    {
                        Text = mesaj,
                        Foreground = Avalonia.Media.Brush.Parse("#B0D8F0"),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 13
                    }
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Children = { kapat }
                }
            }
        };

        await dialog.ShowDialog(this);
    }

    private void Onizleme3D_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Salon.CizimOgeleri.Count == 0)
        {
            _vm.DurumMesaji = "3D için önce bir şeyler çizin (kapalı zemin → zemin+tavan+duvar).";
            return;
        }
        var pencere = new Salon3DOnizlemeWindow(_vm.Salon);
        pencere.Show(this);
    }

    private void GeriAl_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => GeriAl();

    private void Yinele_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Yinele();

    private void Sigdir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CizimCanvas?.Sigdir();

    private void GorunumSifirla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CizimCanvas?.GorunumuSifirla();

    // ── Dosya işlemleri ───────────────────────────────────────────────────────

    private void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.Salon = SalonGeometriServisi.YeniOlustur();
        UndoSifirla();
        _vm.IsDirty = false;
        _vm.DurumMesaji = "Yeni salon geometrisi oluşturuldu.";
        _vm.BasligiYenile();
        SyncPanel();
        BaglaCanvas();
        CizimCanvas?.GorunumuSifirla();
    }

    private async void Sablon_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Mevcut içerik varsa onay al
        bool icerikVar = _vm.Salon.CizimOgeleri.Count > 0 || _vm.Salon.Sahne is not null
            || _vm.Salon.SeyirciSekciyonlari.Count > 0 || _vm.Salon.Balkonlar.Count > 0;
        if (icerikVar)
        {
            var onay = await OnayCal("Mevcut salon şablonla değiştirilecek. Devam edilsin mi?", "Hazır Şablon");
            if (!onay) return;
        }

        var dialog = new SalonSablonWindow();
        await dialog.ShowDialog(this);
        if (dialog.OlusturulanSalon is not { } yeni) return;

        _vm.Salon = yeni;
        UndoSifirla();
        _vm.IsDirty = true; // henüz kaydedilmemiş içerik
        _vm.DurumMesaji = "Şablondan salon oluşturuldu.";
        _vm.BasligiYenile();
        SyncPanel();
        BaglaCanvas();
        CizimCanvas?.Sigdir();
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Salon Geometrisi Aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Salon Geometrisi (.e4salon.json)")
                { Patterns = ["*.e4salon.json"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;
        DosyaAc(path);
    }

    public void DosyaAc(string path)
    {
        try
        {
            _vm.Salon = SalonGeometriServisi.Ac(path);
            UndoSifirla();
            _vm.IsDirty = false;
            _vm.DurumMesaji = $"Açıldı: {Path.GetFileName(path)}  (Rev. {_vm.Salon.GeometriRevizyon})";
            _vm.BasligiYenile();
            SyncPanel();
            BaglaCanvas();
            CizimCanvas?.Sigdir();
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Açılamadı: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await Kaydet();

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydet();

    private async Task<bool> Kaydet()
    {
        if (string.IsNullOrWhiteSpace(_vm.Salon.DosyaYolu))
            return await FarkliKaydet();
        return KaydetYola(_vm.Salon.DosyaYolu);
    }

    private async Task<bool> FarkliKaydet()
    {
        var ad = string.IsNullOrWhiteSpace(_vm.Salon.SalonAdi) ? "Salon" : _vm.Salon.SalonAdi;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salon Geometrisi Kaydet",
            DefaultExtension = "e4salon.json",
            SuggestedFileName = $"{ad}.e4salon.json",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Salon Geometrisi (.e4salon.json)")
                { Patterns = ["*.e4salon.json"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return false;
        return KaydetYola(path);
    }

    private bool KaydetYola(string path)
    {
        try
        {
            SalonGeometriServisi.Kaydet(_vm.Salon, path);
            _vm.IsDirty = false;
            _vm.DurumMesaji = $"Kaydedildi: {Path.GetFileName(path)}  (Rev. {_vm.Salon.GeometriRevizyon})";
            _vm.BasligiYenile();
            return true;
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Kaydedilemedi: {ex.Message}";
            return false;
        }
    }

    // ── Salon adı / yükseklik ─────────────────────────────────────────────────

    private void SalonAdi_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = SalonAdiText?.Text ?? "";
        if (yeni == _vm.Salon.SalonAdi) return;
        SnapshotAl();
        _vm.Salon.SalonAdi = yeni;
        _vm.BasligiYenile();
    }

    private void Yukseklik_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaYukseklik();

    private void Yukseklik_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaYukseklik();
    }

    private void UygulaYukseklik()
    {
        if (double.TryParse(YukseklikText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v) && v > 0 && Math.Abs(v - _vm.SalonYuksekligiM) > 1e-12)
        {
            SnapshotAl();
            _vm.SalonYuksekligiM = v;
        }
        if (YukseklikText is { } tb) tb.Text = _vm.SalonYuksekligiM.ToString("F2");
    }

    // ── Katman ────────────────────────────────────────────────────────────────

    private void KatmanEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapshotAl();
        var katman = SalonKatman.Olustur(SalonKatmanTipi.Genel);
        katman.Ad = $"Katman {_vm.Salon.Katmanlar.Count + 1}";
        _vm.Salon.Katmanlar.Add(katman);
        _vm.SeciliKatman = katman;
        _vm.DurumMesaji = $"'{katman.Ad}' katmanı eklendi.";
    }

    // ── Navigasyon / kapanış ──────────────────────────────────────────────────

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (CizimCanvas is not { } c) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Yinele(); e.Handled = true; return;
                case Key.Z:
                    GeriAl(); e.Handled = true; return;
                case Key.Y:
                    Yinele(); e.Handled = true; return;
                case Key.S:
                    _ = Kaydet(); e.Handled = true; return;
            }
        }

        switch (e.Key)
        {
            case Key.Escape:
                c.CizimiIptal();
                _vm.DurumMesaji = "Çizim iptal edildi.";
                e.Handled = true;
                break;
            case Key.Return or Key.Enter:
                c.PolylineBitir();
                e.Handled = true;
                break;
            case Key.Delete or Key.Back:
                c.SeciliyiSil();
                e.Handled = true;
                break;
            case Key.F3:
                Snap_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F8:
                Orto_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F9:
                Grid_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F10:
                Polar_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;

            // Araç kısayolları (tek harf) — çizim sırasında sayı girişi canvas'ta
            // yakalandığından harfler güvenle araca gider.
            case Key.V: AracSecKisayol(SalonAraci.Sec); e.Handled = true; break;
            case Key.L: AracSecKisayol(SalonAraci.Cizgi); e.Handled = true; break;
            case Key.P: AracSecKisayol(SalonAraci.Polyline); e.Handled = true; break;
            case Key.R: AracSecKisayol(SalonAraci.Dikdortgen); e.Handled = true; break;
            case Key.C: AracSecKisayol(SalonAraci.Daire); e.Handled = true; break;
            case Key.A: AracSecKisayol(SalonAraci.Yay); e.Handled = true; break;
            case Key.T: AracSecKisayol(SalonAraci.Trim); e.Handled = true; break;
            case Key.E: AracSecKisayol(SalonAraci.Uzat); e.Handled = true; break;
            case Key.M: AracSecKisayol(SalonAraci.Tasi); e.Handled = true; break;
            case Key.O: AracSecKisayol(SalonAraci.Offset); e.Handled = true; break;
            case Key.N: AracSecKisayol(SalonAraci.Aynala); e.Handled = true; break;
            case Key.D: AracSecKisayol(SalonAraci.Dondur); e.Handled = true; break;
            case Key.X: AracSecKisayol(SalonAraci.Olcum); e.Handled = true; break;
        }
    }

    private async void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KapanisOnayiAl()) return;
        var hedef = _geriPencere?.Invoke() ?? new EngineeringHomeWindow();
        OpenWindowOnSameScreen(hedef);
    }

    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = Screens?.ScreenFromWindow(this);
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
            win.Position = Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        _kapanisOnaylandi = true;
        Close();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;
        e.Cancel = true;
        if (!await KapanisOnayiAl()) return;
        _kapanisOnaylandi = true;
        Close();
    }

    private enum KapanisSecimi { Iptal, KaydetVeCik, KaydetmedenCik }

    private async Task<bool> KapanisOnayiAl()
    {
        if (!_vm.IsDirty) return true; // kaydedilmemiş değişiklik yok → sormadan kapan
        var secim = await KapanisSecimiSor();
        return secim switch
        {
            KapanisSecimi.KaydetVeCik => await Kaydet(),
            KapanisSecimi.KaydetmedenCik => true,
            _ => false
        };
    }

    private async Task<KapanisSecimi> KapanisSecimiSor()
    {
        var dialog = new Window
        {
            Title = "Modülü Kapat",
            Width = 460, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var sonuc = KapanisSecimi.Iptal;
        var kaydet = DialogButton("Kaydet ve Çık", "#0E5B82", "#1A8BBF", Avalonia.Media.Brushes.White, 130);
        var kaydetmeden = DialogButton("Kaydetmeden Çık", "#3A1010", "#7A2020", Avalonia.Media.Brush.Parse("#F0B0B0"), 145);
        var iptal = DialogButton("İptal", "#0D2535", "#1A5070", Avalonia.Media.Brush.Parse("#C8E4F4"), 90);

        kaydet.Click += (_, _) => { sonuc = KapanisSecimi.KaydetVeCik; dialog.Close(); };
        kaydetmeden.Click += (_, _) => { sonuc = KapanisSecimi.KaydetmedenCik; dialog.Close(); };
        iptal.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = "Modülü kapatmak istediğinize emin misiniz?",
                    Foreground = Avalonia.Media.Brush.Parse("#B0D8F0"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 12,
                    Children = { kaydet, kaydetmeden, iptal }
                }
            }
        };

        await dialog.ShowDialog(this);
        return sonuc;
    }

    private async Task<bool> OnayCal(string mesaj, string baslik)
    {
        var dialog = new Window
        {
            Title = baslik,
            Width = 400, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        bool sonuc = false;
        var evet = DialogButton("Evet", "#0E5B82", "#1A8BBF", Avalonia.Media.Brushes.White, 90);
        var hayir = DialogButton("Hayır", "#0D2535", "#1A5070", Avalonia.Media.Brush.Parse("#C8E4F4"), 90);
        evet.Click += (_, _) => { sonuc = true; dialog.Close(); };
        hayir.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = mesaj,
                    Foreground = Avalonia.Media.Brush.Parse("#B0D8F0"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 13.5
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 12,
                    Children = { evet, hayir }
                }
            }
        };

        await dialog.ShowDialog(this);
        return sonuc;
    }

    private static Button DialogButton(string text, string background, string border,
        Avalonia.Media.IBrush foreground, double width) => new()
    {
        Content = text,
        Width = width,
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        Background = Avalonia.Media.Brush.Parse(background),
        BorderBrush = Avalonia.Media.Brush.Parse(border),
        Foreground = foreground,
        Padding = new Thickness(14, 8),
        CornerRadius = new Avalonia.CornerRadius(6)
    };
}
