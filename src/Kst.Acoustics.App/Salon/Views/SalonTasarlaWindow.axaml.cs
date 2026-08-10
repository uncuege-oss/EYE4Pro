using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Kst.Acoustics.App.Salon.ViewModels;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Common.Geometry;
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
    private string _kayitliIcerikJson = "";
    private readonly SalonEditorAyarlari _editorAyarlari;
    private readonly DispatcherTimer _otomatikKayitZamanlayici;

    // Panel girdileri modelden bilinçli olarak ayrı taslaklardır. Kaydet/kapat sırasında
    // sessizce kaybolmamaları için hangi editör grubunun kullanıcı tarafından değiştiğini izleriz.
    private readonly System.Collections.Generic.HashSet<string> _kirliTaslakGruplari = new();
    private int _panelSenkronSayaci;
    private bool _taslakTakibiHazir;

    // Geri butonunun döneceği pencere fabrikası — her launcher kendi tipini iletir.
    private readonly Func<Window>? _geriPencere;

    // Parameterless ctor: Avalonia AXAML loader için zorunlu.
    public SalonTasarlaWindow() : this(null) { }

    public SalonTasarlaWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = _vm;
        _geriPencere = geriPencere;
        _editorAyarlari = SalonEditorAyarServisi.Yukle();
        _otomatikKayitZamanlayici = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_editorAyarlari.OtomatikKayitDakika)
        };
        _otomatikKayitZamanlayici.Tick += OtomatikKaydet_Tick;
        Opened += OnOpened;
        Closing += OnWindowClosing;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        PanelSenkronize(SyncPanel);
        _kayitliIcerikJson = IcerikJson(_vm.Salon);

        if (CizimCanvas is { } c)
        {
            c.OnDurum = m => _vm.DurumMesaji = m;
            c.OnKoordinat = m => _vm.KoordinatMesaji = m;
            c.OnSecimDegisti = _ => { };
            c.OnDegisti = ModelDegisti;
            c.OnDegisecek = SnapshotAl;
            c.OnAciklikYerlestir = AciklikYerlestir;
            BaglaCanvas();
            c.AyarlariUygula(_editorAyarlari);
            AracIsaretle(AracSec);
            c.Arac = SalonAraci.Sec;
        }

        _vm.PropertyChanged += (_, ev) =>
        {
            switch (ev.PropertyName)
            {
                case nameof(SalonTasarlaViewModel.SeciliKatman):
                    if (CizimCanvas is { } cc) cc.AktifKatman = _vm.SeciliKatman;
                    PanelSenkronize(SyncKatmanDuzenle);
                    break;
                case nameof(SalonTasarlaViewModel.SeciliSeyirci):
                    PanelSenkronize(SyncSeyirciAlanlari);
                    break;
                case nameof(SalonTasarlaViewModel.SeciliBalkon):
                    PanelSenkronize(SyncBalkonAlanlari);
                    break;
                case nameof(SalonTasarlaViewModel.SeciliAciklik):
                    PanelSenkronize(SyncAciklikAlanlari);
                    break;
            }
        };
        PanelSenkronize(SyncKatmanDuzenle);
        TaslakTakibiniBaslat();
        UndoDurumunuYenile();
        _otomatikKayitZamanlayici.Start();
    }

    private void OtomatikKaydet_Tick(object? sender, EventArgs e)
    {
        if (!_vm.IsDirty || _vm.UygulanmamisTaslakVar) return;
        try
        {
            SalonSurumGecmisiServisi.OtomatikKaydet(
                _vm.Salon, SalonSurumGecmisiServisi.VarsayilanKlasor);
            _vm.DurumMesaji = $"Otomatik kurtarma kaydı alındı ({DateTime.Now:HH:mm}).";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = "Otomatik kayıt alınamadı: " + ex.Message;
        }
    }

    private void EditorAyarlariniKaydet()
    {
        try
        {
            CizimCanvas?.AyarlariOku(_editorAyarlari);
            SalonEditorAyarServisi.Kaydet(_editorAyarlari);
        }
        catch (Exception ex) { _vm.DurumMesaji = "Editör ayarları kaydedilemedi: " + ex.Message; }
    }

    // ── Model baseline / panel taslağı ─────────────────────────────────────────

    /// Dirty karşılaştırmasında belge kimliği ve kayıt revizyonu geometri içeriği değildir.
    private static string IcerikJson(SalonGeometri salon)
    {
        var kok = JsonNode.Parse(SalonGeometriServisi.KopyaJson(salon))?.AsObject();
        if (kok is null) return "";
        kok.Remove(nameof(SalonGeometri.DosyaYolu));
        kok.Remove(nameof(SalonGeometri.GeometriRevizyon));
        return kok.ToJsonString();
    }

    private void DirtyYenile()
        => _vm.IsDirty = !string.Equals(IcerikJson(_vm.Salon), _kayitliIcerikJson, StringComparison.Ordinal);

    private void ModelDegisti()
    {
        DirtyYenile();
        CizimCanvas?.InvalidateVisual();
    }

    private void KayitBaselineYenile()
    {
        _kayitliIcerikJson = IcerikJson(_vm.Salon);
        DirtyYenile();
    }

    private void UndoDurumunuYenile()
    {
        _vm.CanUndo = _geriYigini.Count > 0;
        _vm.CanRedo = _ileriYigini.Count > 0;
    }

    /// Panel işlemini atomik uygular; gerçek içerik değişmediyse undo/dirty üretmez.
    private bool ModeliDegistir(Action mutasyon)
    {
        var onceJson = SalonGeometriServisi.KopyaJson(_vm.Salon);
        var onceIcerik = IcerikJson(_vm.Salon);
        mutasyon();
        if (string.Equals(onceIcerik, IcerikJson(_vm.Salon), StringComparison.Ordinal))
            return false;

        _geriYigini.Add(onceJson);
        if (_geriYigini.Count > UndoLimiti) _geriYigini.RemoveAt(0);
        _ileriYigini.Clear();
        UndoDurumunuYenile();
        ModelDegisti();
        return true;
    }

    private void PanelSenkronize(Action eylem)
    {
        _panelSenkronSayaci++;
        try { eylem(); }
        finally { _panelSenkronSayaci--; }
    }

    private void TaslakTakibiniBaslat()
    {
        void Takip(string grup, params AvaloniaObject?[] kontroller)
        {
            foreach (var kontrol in kontroller.OfType<AvaloniaObject>())
            {
                kontrol.PropertyChanged += (_, ev) =>
                {
                    if (!_taslakTakibiHazir || _panelSenkronSayaci > 0) return;
                    bool degerDegisti = kontrol switch
                    {
                        TextBox => ev.Property == TextBox.TextProperty,
                        ComboBox => ev.Property == ComboBox.SelectedIndexProperty,
                        ToggleButton => ev.Property == ToggleButton.IsCheckedProperty,
                        _ => false
                    };
                    if (!degerDegisti) return;
                    _kirliTaslakGruplari.Add(grup);
                    TaslakDurumunuYenile();
                };
            }
        }

        Takip("Katman", KatmanAdText, KatmanRenkText, KatmanTipCombo);
        Takip("Sahne", SahneTipCombo, SahneKonumCombo, SahneGenislikText, SahneDerinlikText,
            SahneYukseklikText, SahneMerkezXText, SahneMerkezYText, SahneKotText);
        Takip("Orkestra çukuru", CukurGenislikText, CukurDerinlikText, CukurDusumText,
            CukurMerkezXText, CukurMerkezYText);
        Takip("Seyirci", SeyirciGenislikText, SeyirciDerinlikText, SeyirciKademeText,
            SeyirciYukText, SeyirciKotText, SeyirciMerkezXText, SeyirciMerkezYText, SeyirciTersCheck,
            KoltukGenislikText, SiraAraligiText, YanKoridorText, OrtaKoridorSayisiText,
            OrtaKoridorGenislikText, ErisilebilirYerText);
        Takip("Balkon / Loca", BalkonTipCombo, BalkonGenislikText, BalkonDerinlikText,
            BalkonKotText, BalkonKalinlikText, BalkonParapetText, BalkonMerkezXText, BalkonMerkezYText);
        Takip("Açıklık", AciklikTipCombo, AciklikGenText, AciklikYukText, AciklikKotText,
            AciklikMerkezXText, AciklikMerkezYText);
        Takip("Tavan profili", TavanYText, TavanKotText);
        _taslakTakibiHazir = true;
    }

    private void TaslakTemizle(string grup)
    {
        _kirliTaslakGruplari.Remove(grup);
        TaslakDurumunuYenile();
    }

    private void TumTaslaklariTemizle()
    {
        _kirliTaslakGruplari.Clear();
        TaslakDurumunuYenile();
    }

    private void TaslakDurumunuYenile()
    {
        _vm.UygulanmamisTaslakVar = _kirliTaslakGruplari.Count > 0;
        // Seçim değişimi taslağı sessizce ezmesin; taslak uygulanana kadar ilgili liste sabitlenir.
        if (KatmanList is { } kl) kl.IsEnabled = !_kirliTaslakGruplari.Contains("Katman");
        if (SeyirciList is { } sl) sl.IsEnabled = !_kirliTaslakGruplari.Contains("Seyirci");
        if (BalkonList is { } bl) bl.IsEnabled = !_kirliTaslakGruplari.Contains("Balkon / Loca");
        if (AciklikList is { } al) al.IsEnabled = !_kirliTaslakGruplari.Contains("Açıklık");
        if (TavanList is { } tl) tl.IsEnabled = !_kirliTaslakGruplari.Contains("Tavan profili");
    }

    private bool TaslakKaydaHazirMi()
    {
        if (_kirliTaslakGruplari.Count == 0) return true;
        string bolumler = string.Join(", ", _kirliTaslakGruplari.OrderBy(x => x));
        _vm.DurumMesaji = $"Önce panel taslağını uygulayın veya geri alın: {bolumler}.";
        return false;
    }

    private void TaslakVazgec_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PanelSenkronize(() =>
        {
            if (_kirliTaslakGruplari.Contains("Katman")) SyncKatmanDuzenle();
            if (_kirliTaslakGruplari.Contains("Sahne")) SyncSahnePanel();
            else if (_kirliTaslakGruplari.Contains("Orkestra çukuru")) SyncCukurPanel();
            if (_kirliTaslakGruplari.Contains("Seyirci")) SyncSeyirciAlanlari();
            if (_kirliTaslakGruplari.Contains("Balkon / Loca")) SyncBalkonAlanlari();
            if (_kirliTaslakGruplari.Contains("Açıklık")) SyncAciklikAlanlari();
            if (_kirliTaslakGruplari.Contains("Tavan profili"))
            {
                if (TavanYText is { } y) y.Text = "";
                if (TavanKotText is { } k) k.Text = "";
            }
        });
        TumTaslaklariTemizle();
        _vm.DurumMesaji = "Uygulanmamış panel değişiklikleri geri alındı.";
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    /// Model değişmeden hemen önce çağrılır: mevcut durumu undo yığınına atar.
    private void SnapshotAl()
    {
        _geriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        if (_geriYigini.Count > UndoLimiti) _geriYigini.RemoveAt(0);
        _ileriYigini.Clear();
        UndoDurumunuYenile();
    }

    private void UndoSifirla()
    {
        _geriYigini.Clear();
        _ileriYigini.Clear();
        UndoDurumunuYenile();
    }

    private void GeriAl()
    {
        if (_geriYigini.Count == 0) { _vm.DurumMesaji = "Geri alınacak işlem yok."; return; }
        _ileriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        var json = _geriYigini[^1];
        _geriYigini.RemoveAt(_geriYigini.Count - 1);
        SalonuGeriYukle(json);
        UndoDurumunuYenile();
        _vm.DurumMesaji = "Geri alındı (Ctrl+Z).";
    }

    private void Yinele()
    {
        if (_ileriYigini.Count == 0) { _vm.DurumMesaji = "Yinelenecek işlem yok."; return; }
        _geriYigini.Add(SalonGeometriServisi.KopyaJson(_vm.Salon));
        if (_geriYigini.Count > UndoLimiti) _geriYigini.RemoveAt(0);
        var json = _ileriYigini[^1];
        _ileriYigini.RemoveAt(_ileriYigini.Count - 1);
        SalonuGeriYukle(json);
        UndoDurumunuYenile();
        _vm.DurumMesaji = "Yinelendi (Ctrl+Y).";
    }

    private void SalonuGeriYukle(string json)
    {
        var dosyaYolu = _vm.Salon.DosyaYolu;
        var revizyon = _vm.Salon.GeometriRevizyon;
        var geri = SalonGeometriServisi.KopyadanYukle(json);
        geri.DosyaYolu = dosyaYolu;
        geri.GeometriRevizyon = revizyon;
        _vm.Salon = geri;
        _vm.BasligiYenile();
        TumTaslaklariTemizle();
        PanelSenkronize(SyncPanel);
        BaglaCanvas();
        DirtyYenile();
        CizimCanvas?.InvalidateVisual();
    }

    // ── Katman yönetimi ───────────────────────────────────────────────────────

    private void SyncKatmanDuzenle()
    {
        var k = _vm.SeciliKatman;
        if (KatmanAdText is { } a) a.Text = k?.Ad ?? "";
        if (KatmanRenkText is { } r) r.Text = k?.RenkHex ?? "";
        if (KatmanTipCombo is { } c) c.SelectedIndex = k is not null ? (int)k.Tip : 0;
        TaslakTemizle("Katman");
    }

    private void OgeyiKatmanaAta_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        if (!k.Gorunur) { _vm.DurumMesaji = "Gizli katmana öğe atanamaz; önce katmanı görünür yapın."; return; }
        CizimCanvas?.SeciliyeKatmanAta(k.Id);
    }

    private void KatmanGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        var ad = KatmanAdText?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(ad)) { _vm.DurumMesaji = "Katman adı boş olamaz."; return; }
        var hex = KatmanRenkText?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(hex)) { _vm.DurumMesaji = "Katman rengi boş olamaz (#RRGGBB)."; return; }
        try { _ = Avalonia.Media.Color.Parse(hex); }
        catch { _vm.DurumMesaji = "Geçersiz renk (#RRGGBB bekleniyor)."; return; }

        int tipIndex = KatmanTipCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(SalonKatmanTipi), tipIndex))
        { _vm.DurumMesaji = "Geçerli bir katman tipi seçin."; return; }
        var tip = (SalonKatmanTipi)tipIndex;

        bool degisti = ModeliDegistir(() => { k.Ad = ad; k.RenkHex = hex; k.Tip = tip; });
        TaslakTemizle("Katman");
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = degisti ? $"Katman güncellendi: {k.Ad}" : "Katman zaten güncel.";
    }

    private async void KatmanSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKatman is not { } k) { _vm.DurumMesaji = "Önce bir katman seçin."; return; }
        if (_vm.Katmanlar.Count <= 1) { _vm.DurumMesaji = "En az bir katman kalmalı."; return; }
        var hedef = _vm.Katmanlar.FirstOrDefault(x => !ReferenceEquals(x, k) && x.Gorunur)
                    ?? _vm.Katmanlar.First(x => !ReferenceEquals(x, k));
        int tasinan = _vm.Salon.CizimOgeleri.Count(o => o.KatmanId == k.Id);
        string soru = tasinan > 0
            ? $"'{k.Ad}' silinecek ve {tasinan} öğe '{hedef.Ad}' katmanına taşınacak. Devam edilsin mi?"
            : $"'{k.Ad}' katmanı silinsin mi?";
        if (!await OnayCal(soru, "Katman Sil")) return;

        ModeliDegistir(() =>
        {
            foreach (var oge in _vm.Salon.CizimOgeleri)
                if (oge.KatmanId == k.Id) oge.KatmanId = hedef.Id;
            _vm.Katmanlar.Remove(k);
            _vm.SeciliKatman = hedef;
        });
        TaslakTemizle("Katman");
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = tasinan > 0
            ? $"Katman silindi; {tasinan} öğe '{hedef.Ad}' katmanına taşındı."
            : "Katman silindi.";
    }

    private void KatmanGorunurluk_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: SalonKatman katman } kutu) return;
        bool gorunur = kutu.IsChecked == true;
        if (katman.Gorunur == gorunur) return;

        ModeliDegistir(() => katman.Gorunur = gorunur);
        if (!gorunur && ReferenceEquals(_vm.SeciliKatman, katman))
            _vm.SeciliKatman = _vm.Katmanlar.FirstOrDefault(k => k.Gorunur);
        CizimCanvas?.InvalidateVisual();
        _vm.DurumMesaji = gorunur
            ? $"'{katman.Ad}' görünür."
            : $"'{katman.Ad}' gizlendi; gizli öğeler 3D/DXF/M01 çıktısına alınmaz.";
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
        SyncSeyirciAlanlari();
        SyncBalkonAlanlari();
        SyncAciklikAlanlari();
        if (TavanYText is { } ty) ty.Text = "";
        if (TavanKotText is { } tk) tk.Text = "";
        TaslakTemizle("Tavan profili");
    }

    private void Faset_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (int.TryParse(FasetText?.Text, out int v))
        {
            v = Math.Clamp(v, 12, 128);
            if (v != _vm.Salon.FasetKenarSayisi)
            {
                ModeliDegistir(() => _vm.Salon.FasetKenarSayisi = v);
                _vm.DurumMesaji = $"Faset kenar sayısı: {v} (daire/oval 3D-DXF çözünürlüğü).";
            }
        }
        else if (!string.IsNullOrWhiteSpace(FasetText?.Text))
            _vm.DurumMesaji = "Faset sayısı 12–128 arasında tam sayı olmalı.";
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
        TaslakTemizle("Sahne");
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
        TaslakTemizle("Orkestra çukuru");
    }

    private void CukurUygula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var mevcut = _vm.Salon.OrkestraCukuru;
        if (!PozitifOku(CukurGenislikText, "Çukur genişliği", mevcut?.GenislikM ?? 12.0, out double genislik)
            || !PozitifOku(CukurDerinlikText, "Çukur derinliği", mevcut?.DerinlikM ?? 3.0, out double derinlik)
            || !PozitifOku(CukurDusumText, "Çukur düşümü", mevcut?.CukurDerinligiM ?? 2.5, out double dusum))
            return;

        // Varsayılan yerleşim: sahnenin hemen önü (seyirci tarafı = +Y kenarı), yoksa orijin.
        double dcx = 0, dcy = 0;
        if (_vm.Salon.Sahne is { } sahne)
        {
            dcx = sahne.MerkezX;
            dcy = sahne.MerkezY + sahne.DerinlikM / 2 + derinlik / 2;
        }
        if (!SayiOku(CukurMerkezXText, "Çukur merkez X", mevcut?.MerkezX ?? dcx, out double merkezX)
            || !SayiOku(CukurMerkezYText, "Çukur merkez Y", mevcut?.MerkezY ?? dcy, out double merkezY))
            return;

        var c = new OrkestraCukuru
        {
            GenislikM = genislik, DerinlikM = derinlik, CukurDerinligiM = dusum,
            MerkezX = merkezX, MerkezY = merkezY
        };
        ModeliDegistir(() => _vm.Salon.OrkestraCukuru = c);
        PanelSenkronize(SyncCukurPanel);
        _vm.DurumMesaji = $"Orkestra çukuru: {c.GenislikM:F1}×{c.DerinlikM:F1} m · −{c.CukurDerinligiM:F1} m";
    }

    private void CukurKaldir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Salon.OrkestraCukuru is null) return;
        ModeliDegistir(() => _vm.Salon.OrkestraCukuru = null);
        PanelSenkronize(SyncCukurPanel);
        _vm.DurumMesaji = "Orkestra çukuru kaldırıldı.";
    }

    /// Seçili seyirci alanının değerlerini giriş kutularına doldurur (Güncelle akışı).
    private void SyncSeyirciAlanlari()
    {
        var s = _vm.SeciliSeyirci;
        if (SeyirciGenislikText is { } g) g.Text = s?.GenislikM.ToString("F2") ?? "";
        if (SeyirciDerinlikText is { } d) d.Text = s?.DerinlikM.ToString("F2") ?? "";
        if (SeyirciKademeText is { } k) k.Text = s?.KademeSayisi.ToString() ?? "";
        if (SeyirciYukText is { } y) y.Text = s?.KademeYuksekligiM.ToString("F2") ?? "";
        if (SeyirciKotText is { } kt) kt.Text = s?.TabanKotuM.ToString("F2") ?? "";
        if (SeyirciMerkezXText is { } mx) mx.Text = s?.MerkezX.ToString("F2") ?? "";
        if (SeyirciMerkezYText is { } my) my.Text = s?.MerkezY.ToString("F2") ?? "";
        if (KoltukGenislikText is { } kg) kg.Text = s?.KoltukGenisligiM.ToString("F2") ?? "0.55";
        if (SiraAraligiText is { } sa) sa.Text = s?.SiraAraligiM.ToString("F2") ?? "0.90";
        if (YanKoridorText is { } yk) yk.Text = s?.YanKoridorGenisligiM.ToString("F2") ?? "1.20";
        if (OrtaKoridorSayisiText is { } oks) oks.Text = s?.OrtaKoridorSayisi.ToString() ?? "1";
        if (OrtaKoridorGenislikText is { } okg) okg.Text = s?.OrtaKoridorGenisligiM.ToString("F2") ?? "1.20";
        if (ErisilebilirYerText is { } ey) ey.Text = s?.TekerlekliSandalyeYeri.ToString() ?? "2";
        if (SeyirciTersCheck is { } t) t.IsChecked = s?.TersYon ?? false;
        TaslakTemizle("Seyirci");
    }

    /// Seçili balkonun değerlerini giriş kutularına doldurur (Güncelle akışı).
    private void SyncBalkonAlanlari()
    {
        var b = _vm.SeciliBalkon;
        if (BalkonTipCombo is { } tc) tc.SelectedIndex = b is null ? 0 : (int)b.Tip;
        if (BalkonGenislikText is { } g) g.Text = b?.GenislikM.ToString("F2") ?? "";
        if (BalkonDerinlikText is { } d) d.Text = b?.DerinlikM.ToString("F2") ?? "";
        if (BalkonKotText is { } k) k.Text = b?.TabanKotuM.ToString("F2") ?? "";
        if (BalkonKalinlikText is { } kl) kl.Text = b?.KalinlikM.ToString("F2") ?? "";
        if (BalkonParapetText is { } p) p.Text = b?.ParapetM.ToString("F2") ?? "";
        if (BalkonMerkezXText is { } mx) mx.Text = b?.MerkezX.ToString("F2") ?? "";
        if (BalkonMerkezYText is { } my) my.Text = b?.MerkezY.ToString("F2") ?? "";
        TaslakTemizle("Balkon / Loca");
    }

    private void SahneUygula_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var mevcut = _vm.Salon.Sahne;
        int tipIndex = SahneTipCombo?.SelectedIndex ?? -1;
        int konumIndex = SahneKonumCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(SahneTipi), tipIndex) || !Enum.IsDefined(typeof(SahneKonum), konumIndex))
        { _vm.DurumMesaji = "Geçerli sahne tipi ve konumu seçin."; return; }
        if (!PozitifOku(SahneGenislikText, "Sahne genişliği", mevcut?.GenislikM ?? 12.0, out double genislik)
            || !PozitifOku(SahneDerinlikText, "Sahne derinliği", mevcut?.DerinlikM ?? 8.0, out double derinlik)
            || !PozitifOku(SahneYukseklikText, "Sahne yüksekliği", mevcut?.YukseklikM ?? 1.0, out double yukseklik)
            || !SayiOku(SahneKotText, "Sahne taban kotu", mevcut?.TabanKotuM ?? 0, out double tabanKotu))
            return;

        var s = new SahneGeometri
        {
            Tip = (SahneTipi)tipIndex, Konum = (SahneKonum)konumIndex,
            GenislikM = genislik, DerinlikM = derinlik, YukseklikM = yukseklik, TabanKotuM = tabanKotu
        };

        var (dcx, dcy) = VarsayilanSahneMerkezi(s);
        if (!SayiOku(SahneMerkezXText, "Sahne merkez X", mevcut?.MerkezX ?? dcx, out double merkezX)
            || !SayiOku(SahneMerkezYText, "Sahne merkez Y", mevcut?.MerkezY ?? dcy, out double merkezY))
            return;
        s.MerkezX = merkezX;
        s.MerkezY = merkezY;

        ModeliDegistir(() => _vm.Salon.Sahne = s);
        PanelSenkronize(SyncSahnePanel);
        _vm.DurumMesaji = $"Sahne: {SahneTipAd(s.Tip)} · {s.GenislikM:F1}×{s.DerinlikM:F1} m · yük {s.YukseklikM:F1} m";
    }

    private void SahneKaldir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.Salon.Sahne is null) return;
        ModeliDegistir(() => _vm.Salon.Sahne = null);
        PanelSenkronize(SyncSahnePanel);
        _vm.DurumMesaji = "Sahne kaldırıldı.";
    }

    private static string SahneTipAd(SahneTipi t) => t switch
    {
        SahneTipi.Dairesel => "Dairesel",
        SahneTipi.Oval => "Oval",
        _ => "Dikdörtgen"
    };

    private bool PozitifOku(TextBox? tb, string alan, double varsayilan, out double deger)
    {
        if (string.IsNullOrWhiteSpace(tb?.Text)) { deger = varsayilan; return true; }
        if (SayiOku(tb) is { } v && double.IsFinite(v) && v > 0)
        { deger = v; return true; }
        deger = varsayilan;
        _vm.DurumMesaji = $"{alan} için sıfırdan büyük geçerli bir sayı girin.";
        tb?.Focus();
        return false;
    }

    private bool AltSinirliOku(TextBox? tb, string alan, double varsayilan, double altSinir, out double deger)
    {
        if (PozitifOku(tb, alan, varsayilan, out deger) && deger >= altSinir) return true;
        _vm.DurumMesaji = $"{alan} en az {altSinir:0.##} m olmalıdır.";
        tb?.Focus();
        return false;
    }

    private bool SayiOku(TextBox? tb, string alan, double varsayilan, out double deger)
    {
        if (string.IsNullOrWhiteSpace(tb?.Text)) { deger = varsayilan; return true; }
        if (SayiOku(tb) is { } v && double.IsFinite(v)) { deger = v; return true; }
        deger = varsayilan;
        _vm.DurumMesaji = $"{alan} için geçerli bir sayı girin.";
        tb?.Focus();
        return false;
    }

    private bool NegatifOlmayanOku(TextBox? tb, string alan, double varsayilan, out double deger)
    {
        if (!SayiOku(tb, alan, varsayilan, out deger)) return false;
        if (deger >= 0) return true;
        _vm.DurumMesaji = $"{alan} negatif olamaz.";
        tb?.Focus();
        return false;
    }

    private bool PozitifTamSayiOku(TextBox? tb, string alan, int varsayilan, out int deger)
    {
        if (!PozitifOku(tb, alan, varsayilan, out double ham)) { deger = varsayilan; return false; }
        if (ham > 10_000 || Math.Abs(ham - Math.Round(ham)) > 1e-9)
        {
            deger = varsayilan;
            _vm.DurumMesaji = $"{alan} için 1–10000 arasında tam sayı girin.";
            tb?.Focus();
            return false;
        }
        deger = (int)Math.Round(ham);
        return true;
    }

    private bool NegatifOlmayanTamSayiOku(TextBox? tb, string alan, int varsayilan, out int deger)
    {
        if (!NegatifOlmayanOku(tb, alan, varsayilan, out double ham)) { deger = varsayilan; return false; }
        if (ham > 10_000 || Math.Abs(ham - Math.Round(ham)) > 1e-9)
        {
            deger = varsayilan;
            _vm.DurumMesaji = $"{alan} için 0–10000 arasında tam sayı girin.";
            tb?.Focus();
            return false;
        }
        deger = (int)Math.Round(ham);
        return true;
    }

    private bool AralikliTamSayiOku(TextBox? tb, string alan, int varsayilan, int min, int max, out int deger)
    {
        if (NegatifOlmayanTamSayiOku(tb, alan, varsayilan, out deger) && deger >= min && deger <= max) return true;
        _vm.DurumMesaji = $"{alan} {min}–{max} arasında olmalıdır.";
        tb?.Focus();
        return false;
    }

    // ── Seyirci alanı ─────────────────────────────────────────────────────────

    private void SeyirciEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!PozitifOku(SeyirciGenislikText, "Seyirci genişliği", 16, out double genislik)
            || !PozitifOku(SeyirciDerinlikText, "Seyirci derinliği", 12, out double derinlik)
            || !PozitifTamSayiOku(SeyirciKademeText, "Kademe sayısı", 6, out int kademe)
            || !PozitifOku(SeyirciYukText, "Kademe yüksekliği", 0.45, out double kademeYuk)
            || !SayiOku(SeyirciKotText, "Seyirci taban kotu", 0, out double tabanKotu)
            || !AltSinirliOku(KoltukGenislikText, "Koltuk genişliği", 0.55, 0.40, out double koltukGen)
            || !AltSinirliOku(SiraAraligiText, "Sıra aralığı", 0.90, 0.60, out double siraAralik)
            || !NegatifOlmayanOku(YanKoridorText, "Yan koridor", 1.20, out double yanKoridor)
            || !AralikliTamSayiOku(OrtaKoridorSayisiText, "Orta koridor sayısı", 1, 0, 8, out int ortaKoridor)
            || !NegatifOlmayanOku(OrtaKoridorGenislikText, "Orta koridor genişliği", 1.20, out double ortaKoridorGen)
            || !AralikliTamSayiOku(ErisilebilirYerText, "Erişilebilir yer sayısı", 2, 0, 100, out int erisilebilir))
            return;
        var sek = new SeyirciSekciyonu
        {
            Ad = $"Seyirci {_vm.SeyirciSekciyonlari.Count + 1}",
            GenislikM = genislik, DerinlikM = derinlik, KademeSayisi = kademe,
            KademeYuksekligiM = kademeYuk, TabanKotuM = tabanKotu,
            KoltukGenisligiM = koltukGen, SiraAraligiM = siraAralik,
            YanKoridorGenisligiM = yanKoridor, OrtaKoridorSayisi = ortaKoridor,
            OrtaKoridorGenisligiM = ortaKoridorGen, TekerlekliSandalyeYeri = erisilebilir,
            TersYon = SeyirciTersCheck?.IsChecked == true
        };

        var (cx, cy) = VarsayilanSeyirciMerkezi(sek);
        if (!SayiOku(SeyirciMerkezXText, "Seyirci merkez X", cx, out double merkezX)
            || !SayiOku(SeyirciMerkezYText, "Seyirci merkez Y", cy, out double merkezY))
            return;
        sek.MerkezX = merkezX;
        sek.MerkezY = merkezY;

        ModeliDegistir(() => _vm.SeyirciSekciyonlari.Add(sek));
        _vm.SeciliSeyirci = sek;
        TaslakTemizle("Seyirci");
        _vm.DurumMesaji = SeyirciOzeti(sek, "eklendi");
    }

    /// Seçili seyirci alanını giriş kutularındaki değerlerle günceller.
    private void SeyirciGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliSeyirci is not { } sek) { _vm.DurumMesaji = "Önce listeden bir seyirci alanı seçin."; return; }
        if (!PozitifOku(SeyirciGenislikText, "Seyirci genişliği", sek.GenislikM, out double genislik)
            || !PozitifOku(SeyirciDerinlikText, "Seyirci derinliği", sek.DerinlikM, out double derinlik)
            || !PozitifTamSayiOku(SeyirciKademeText, "Kademe sayısı", sek.KademeSayisi, out int kademe)
            || !PozitifOku(SeyirciYukText, "Kademe yüksekliği", sek.KademeYuksekligiM, out double kademeYuk)
            || !SayiOku(SeyirciKotText, "Seyirci taban kotu", sek.TabanKotuM, out double tabanKotu)
            || !SayiOku(SeyirciMerkezXText, "Seyirci merkez X", sek.MerkezX, out double merkezX)
            || !SayiOku(SeyirciMerkezYText, "Seyirci merkez Y", sek.MerkezY, out double merkezY)
            || !AltSinirliOku(KoltukGenislikText, "Koltuk genişliği", sek.KoltukGenisligiM, 0.40, out double koltukGen)
            || !AltSinirliOku(SiraAraligiText, "Sıra aralığı", sek.SiraAraligiM, 0.60, out double siraAralik)
            || !NegatifOlmayanOku(YanKoridorText, "Yan koridor", sek.YanKoridorGenisligiM, out double yanKoridor)
            || !AralikliTamSayiOku(OrtaKoridorSayisiText, "Orta koridor sayısı", sek.OrtaKoridorSayisi, 0, 8, out int ortaKoridor)
            || !NegatifOlmayanOku(OrtaKoridorGenislikText, "Orta koridor genişliği", sek.OrtaKoridorGenisligiM, out double ortaKoridorGen)
            || !AralikliTamSayiOku(ErisilebilirYerText, "Erişilebilir yer sayısı", sek.TekerlekliSandalyeYeri, 0, 100, out int erisilebilir))
            return;
        bool ters = SeyirciTersCheck?.IsChecked == true;
        ModeliDegistir(() =>
        {
            sek.GenislikM = genislik; sek.DerinlikM = derinlik; sek.KademeSayisi = kademe;
            sek.KademeYuksekligiM = kademeYuk; sek.TabanKotuM = tabanKotu;
            sek.MerkezX = merkezX; sek.MerkezY = merkezY; sek.TersYon = ters;
            sek.KoltukGenisligiM = koltukGen; sek.SiraAraligiM = siraAralik;
            sek.YanKoridorGenisligiM = yanKoridor; sek.OrtaKoridorSayisi = ortaKoridor;
            sek.OrtaKoridorGenisligiM = ortaKoridorGen; sek.TekerlekliSandalyeYeri = erisilebilir;
            ListeyiTazele(_vm.SeyirciSekciyonlari, sek);
        });
        _vm.SeciliSeyirci = sek;
        TaslakTemizle("Seyirci");
        _vm.DurumMesaji = SeyirciOzeti(sek, "güncellendi");
    }

    private string SeyirciOzeti(SeyirciSekciyonu sek, string eylem)
    {
        double egim = sek.DerinlikM > 0 ? sek.KademeYuksekligiM * sek.KademeSayisi / sek.DerinlikM * 100 : 0;
        var yerlesim = SalonKoltukYerlesimServisi.Uret(sek, _vm.Salon.Sahne);
        return $"{sek.Ad} {eylem} · {sek.KademeSayisi} kademe · ~%{egim:F0} eğim · " +
               $"{yerlesim.NormalKoltukSayisi} koltuk + {yerlesim.TekerlekliSandalyeYeriSayisi} erişilebilir yer";
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
        ModeliDegistir(() => _vm.SeyirciSekciyonlari.Remove(sek));
        _vm.SeciliSeyirci = null;
        TaslakTemizle("Seyirci");
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
        int tipIndex = BalkonTipCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(BalkonTipi), tipIndex)) { _vm.DurumMesaji = "Geçerli balkon/loca tipi seçin."; return; }
        var tip = (BalkonTipi)tipIndex;
        if (!PozitifOku(BalkonGenislikText, "Balkon genişliği", tip == BalkonTipi.Loca ? 4 : 16, out double genislik)
            || !PozitifOku(BalkonDerinlikText, "Balkon derinliği", tip == BalkonTipi.Loca ? 3 : 5, out double derinlik)
            || !SayiOku(BalkonKotText, "Balkon kotu", 5.0, out double kot)
            || !PozitifOku(BalkonKalinlikText, "Balkon kalınlığı", 0.4, out double kalinlik)
            || !NegatifOlmayanOku(BalkonParapetText, "Parapet yüksekliği", 1.0, out double parapet))
            return;
        var b = new BalkonGeometri
        {
            Tip = tip,
            Ad = $"{(tip == BalkonTipi.Loca ? "Loca" : "Balkon")} {_vm.Balkonlar.Count + 1}",
            GenislikM = genislik, DerinlikM = derinlik, TabanKotuM = kot,
            KalinlikM = kalinlik, ParapetM = parapet
        };

        var (cx, cy) = VarsayilanBalkonMerkezi(b);
        if (!SayiOku(BalkonMerkezXText, "Balkon merkez X", cx, out double merkezX)
            || !SayiOku(BalkonMerkezYText, "Balkon merkez Y", cy, out double merkezY))
            return;
        b.MerkezX = merkezX;
        b.MerkezY = merkezY;

        ModeliDegistir(() => _vm.Balkonlar.Add(b));
        _vm.SeciliBalkon = b;
        TaslakTemizle("Balkon / Loca");
        _vm.DurumMesaji = $"{b.Ad} eklendi · kot {b.TabanKotuM:F1} m · parapet {b.ParapetM:F1} m";
    }

    /// Seçili balkon/locayı giriş kutularındaki değerlerle günceller.
    private void BalkonGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliBalkon is not { } b) { _vm.DurumMesaji = "Önce listeden bir balkon/loca seçin."; return; }
        int tipIndex = BalkonTipCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(BalkonTipi), tipIndex)) { _vm.DurumMesaji = "Geçerli balkon/loca tipi seçin."; return; }
        if (!PozitifOku(BalkonGenislikText, "Balkon genişliği", b.GenislikM, out double genislik)
            || !PozitifOku(BalkonDerinlikText, "Balkon derinliği", b.DerinlikM, out double derinlik)
            || !SayiOku(BalkonKotText, "Balkon kotu", b.TabanKotuM, out double kot)
            || !PozitifOku(BalkonKalinlikText, "Balkon kalınlığı", b.KalinlikM, out double kalinlik)
            || !NegatifOlmayanOku(BalkonParapetText, "Parapet yüksekliği", b.ParapetM, out double parapet)
            || !SayiOku(BalkonMerkezXText, "Balkon merkez X", b.MerkezX, out double merkezX)
            || !SayiOku(BalkonMerkezYText, "Balkon merkez Y", b.MerkezY, out double merkezY))
            return;
        ModeliDegistir(() =>
        {
            b.Tip = (BalkonTipi)tipIndex; b.GenislikM = genislik; b.DerinlikM = derinlik;
            b.TabanKotuM = kot; b.KalinlikM = kalinlik; b.ParapetM = parapet;
            b.MerkezX = merkezX; b.MerkezY = merkezY;
            ListeyiTazele(_vm.Balkonlar, b);
        });
        _vm.SeciliBalkon = b;
        TaslakTemizle("Balkon / Loca");
        _vm.DurumMesaji = $"{b.Ad} güncellendi · kot {b.TabanKotuM:F1} m · parapet {b.ParapetM:F1} m";
    }

    private void BalkonSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliBalkon is not { } b) { _vm.DurumMesaji = "Önce listeden bir balkon/loca seçin."; return; }
        ModeliDegistir(() => _vm.Balkonlar.Remove(b));
        _vm.SeciliBalkon = null;
        TaslakTemizle("Balkon / Loca");
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
        int tipIndex = AciklikTipCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(AciklikTipi), tipIndex)) { _vm.DurumMesaji = "Geçerli açıklık tipi seçin."; return; }
        var tip = (AciklikTipi)tipIndex;
        if (!PozitifOku(AciklikGenText, "Açıklık genişliği", tip == AciklikTipi.Pencere ? 1.5 : 1.0, out double genislik)
            || !PozitifOku(AciklikYukText, "Açıklık yüksekliği", tip == AciklikTipi.Pencere ? 1.2 : 2.1, out double yukseklik)
            || !NegatifOlmayanOku(AciklikKotText, "Açıklık alt kotu", tip == AciklikTipi.Pencere ? 1.0 : 0, out double altKot))
            return;
        var ac = new DuvarAcikligi
        {
            Tip = tip,
            Ad = $"{(tip == AciklikTipi.Pencere ? "Pencere" : "Kapı")} {_vm.Acikliklar.Count + 1}",
            GenislikM = genislik, YukseklikM = yukseklik, AltKotM = altKot,
            MerkezX = nokta.X,
            MerkezY = nokta.Y
        };
        ModeliDegistir(() => _vm.Acikliklar.Add(ac));
        _vm.SeciliAciklik = ac;
        TaslakTemizle("Açıklık");
        _vm.DurumMesaji = $"{ac.Ad} yerleştirildi ({ac.GenislikM:F1}×{ac.YukseklikM:F1} m @ +{ac.AltKotM:F1} m).";
    }

    private void SyncAciklikAlanlari()
    {
        var ac = _vm.SeciliAciklik;
        if (AciklikTipCombo is { } t) t.SelectedIndex = ac is null ? 0 : (int)ac.Tip;
        if (AciklikGenText is { } g) g.Text = ac?.GenislikM.ToString("F2") ?? "";
        if (AciklikYukText is { } y) y.Text = ac?.YukseklikM.ToString("F2") ?? "";
        if (AciklikKotText is { } k) k.Text = ac?.AltKotM.ToString("F2") ?? "";
        if (AciklikMerkezXText is { } mx) mx.Text = ac?.MerkezX.ToString("F2") ?? "";
        if (AciklikMerkezYText is { } my) my.Text = ac?.MerkezY.ToString("F2") ?? "";
        TaslakTemizle("Açıklık");
    }

    private void AciklikGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliAciklik is not { } ac) { _vm.DurumMesaji = "Önce listeden bir açıklık seçin."; return; }
        int tipIndex = AciklikTipCombo?.SelectedIndex ?? -1;
        if (!Enum.IsDefined(typeof(AciklikTipi), tipIndex)) { _vm.DurumMesaji = "Geçerli açıklık tipi seçin."; return; }
        if (!PozitifOku(AciklikGenText, "Açıklık genişliği", ac.GenislikM, out double genislik)
            || !PozitifOku(AciklikYukText, "Açıklık yüksekliği", ac.YukseklikM, out double yukseklik)
            || !NegatifOlmayanOku(AciklikKotText, "Açıklık alt kotu", ac.AltKotM, out double altKot)
            || !SayiOku(AciklikMerkezXText, "Açıklık merkez X", ac.MerkezX, out double merkezX)
            || !SayiOku(AciklikMerkezYText, "Açıklık merkez Y", ac.MerkezY, out double merkezY))
            return;
        ModeliDegistir(() =>
        {
            ac.Tip = (AciklikTipi)tipIndex; ac.GenislikM = genislik; ac.YukseklikM = yukseklik;
            ac.AltKotM = altKot; ac.MerkezX = merkezX; ac.MerkezY = merkezY;
            ListeyiTazele(_vm.Acikliklar, ac);
        });
        _vm.SeciliAciklik = ac;
        TaslakTemizle("Açıklık");
        _vm.DurumMesaji = $"{ac.Ad} güncellendi.";
    }

    private void AciklikSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliAciklik is not { } ac) { _vm.DurumMesaji = "Önce listeden bir açıklık seçin."; return; }
        ModeliDegistir(() => _vm.Acikliklar.Remove(ac));
        _vm.SeciliAciklik = null;
        TaslakTemizle("Açıklık");
        _vm.DurumMesaji = "Açıklık silindi.";
    }

    // ── Tavan profili ─────────────────────────────────────────────────────────

    private void TavanNoktaEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        double? y = SayiOku(TavanYText);
        double? kot = SayiOku(TavanKotText);
        if (y is null || !double.IsFinite(y.Value) || kot is null || !double.IsFinite(kot.Value) || kot <= 0)
        {
            _vm.DurumMesaji = "Tavan noktası için Y (m) ve pozitif Kot (m) girin.";
            return;
        }

        ModeliDegistir(() =>
        {
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
        });

        TaslakTemizle("Tavan profili");
        _vm.DurumMesaji = _vm.TavanProfili.Count >= 2
            ? $"Tavan profili: {_vm.TavanProfili.Count} nokta (eğik/parçalı tavan aktif)."
            : "Tavan noktası eklendi — profil için en az 2 nokta gerekir.";
    }

    private void TavanNoktaSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliTavanNoktasi is not { } n) { _vm.DurumMesaji = "Önce listeden bir tavan noktası seçin."; return; }
        ModeliDegistir(() => _vm.TavanProfili.Remove(n));
        _vm.SeciliTavanNoktasi = null;
        _vm.DurumMesaji = _vm.TavanProfili.Count >= 2
            ? "Tavan noktası silindi."
            : "Tavan noktası silindi — profil devre dışı (düz tavan).";
    }

    private void TavanDuz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.TavanProfili.Count == 0) return;
        ModeliDegistir(_vm.TavanProfili.Clear);
        _vm.SeciliTavanNoktasi = null;
        _vm.DurumMesaji = $"Düz tavana dönüldü ({_vm.Salon.SalonYuksekligiM:F1} m).";
    }

    private static double? SayiOku(TextBox? tb)
        => double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v)
            && double.IsFinite(v) ? v : null;

    // ── Araç seçimi (radio toggle) ────────────────────────────────────────────

    private void Arac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;
        if (!Enum.TryParse<SalonAraci>(tag, out var arac)) return;
        AracIsaretle(btn);
        if (CizimCanvas is { } c) { c.Arac = arac; c.Focus(); }
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
        if (aktif && PolarBtn?.IsChecked == true)
        {
            c.PolarToggle();
            PolarBtn.IsChecked = false;
        }
        c.Focus();
    }

    private void Snap_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.SnapToggle();
        if (SnapBtn is { } b) b.IsChecked = aktif;
        c.Focus();
    }

    private void Grid_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.GridToggle();
        if (GridBtn is { } b) b.IsChecked = aktif;
        c.Focus();
    }

    private void Polar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CizimCanvas is not { } c) return;
        bool aktif = c.PolarToggle();
        if (PolarBtn is { } b) b.IsChecked = aktif;
        if (aktif && OrtoBtn?.IsChecked == true)
        {
            c.OrthoToggle();
            OrtoBtn.IsChecked = false;
        }
        c.Focus();
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
        kapat.IsDefault = true;
        kapat.IsCancel = true;
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

    private async void HizliTasarim_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SalonHizliTasarimWindow();
        await dialog.ShowDialog(this);
        if (dialog.Sonuc is not { } sonuc) return;
        if (!await BelgeDegistirmeOnayiAl("Hızlı Tasarım")) return;
        string notlar = sonuc.Notlar.Count == 0 ? "" : " · " + string.Join(" ", sonuc.Notlar.Take(2));
        BelgeyiUygula(sonuc.Salon, kayitli: false,
            $"Hızlı tasarım hazır: {sonuc.UretilenKoltuk} yer, RT60 ort. {sonuc.AkustikOnKontrol.OrtalamaRt60S:0.00} s{notlar}",
            sigdir: true);
    }

    private async void Analiz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SalonAnalizWindow(_vm.Salon, SnapshotAl);
        await dialog.ShowDialog(this);
        if (!dialog.Degisti) return;
        ModelDegisti();
        BaglaCanvas();
        _vm.DurumMesaji = "Analiz penceresindeki yansıtıcı değişiklikleri uygulandı.";
    }

    private async void GelismisCad_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var secim = CizimCanvas?.Secim.ToList() ?? [];
        if (secim.Count == 0) { _vm.DurumMesaji = "CAD+ için önce çizimde bir veya daha fazla öğe seçin."; return; }
        var dialog = new SalonGelismisCadWindow();
        await dialog.ShowDialog(this);
        if (dialog.Istek is not { } istek) return;
        string once = SalonGeometriServisi.KopyaJson(_vm.Salon);
        try
        {
            ModeliDegistir(() => CadIsleminiUygula(istek, secim));
            CizimCanvas?.SecimiTemizle();
            _vm.DurumMesaji = $"CAD+ işlemi uygulandı: {istek.Islem}.";
        }
        catch (Exception ex)
        {
            SalonuGeriYukle(once);
            _vm.DurumMesaji = "CAD+ işlemi uygulanamadı: " + ex.Message;
        }
    }

    private void CadIsleminiUygula(SalonCadIstegi istek, System.Collections.Generic.List<SalonCizimOgesi> secim)
    {
        switch (istek.Islem)
        {
            case SalonCadIslemi.Pah:
            case SalonCadIslemi.Fillet:
            {
                if (secim.Count != 1 || secim[0].Noktalar.Count != 3)
                    throw new InvalidOperationException("Bu işlem için tam üç köşeli tek bir polyline seçin.");
                var o = secim[0];
                var r = istek.Islem == SalonCadIslemi.Pah
                    ? SalonGelismisCadServisi.PahKir(o.Noktalar[0], o.Noktalar[1], o.Noktalar[2], istek.A, o.KatmanId)
                    : SalonGelismisCadServisi.KoseYuvarla(o.Noktalar[0], o.Noktalar[1], o.Noktalar[2], istek.A, o.KatmanId);
                _vm.Salon.CizimOgeleri.Remove(o);
                _vm.Salon.CizimOgeleri.Add(r.Birinci); _vm.Salon.CizimOgeleri.Add(r.Ikinci);
                if (r.Yay is { } ucuncu) _vm.Salon.CizimOgeleri.Add(ucuncu);
                break;
            }
            case SalonCadIslemi.DogrusalDizi:
            {
                var r = SalonGelismisCadServisi.DogrusalDizi(secim, Math.Max(1, istek.C), istek.A, istek.B);
                foreach (var o in r.Skip(secim.Count)) _vm.Salon.CizimOgeleri.Add(o);
                break;
            }
            case SalonCadIslemi.DaireselDizi:
            {
                var r = SalonGelismisCadServisi.DaireselDizi(secim, new Point2D(0, 0), Math.Max(1, istek.C), istek.A);
                foreach (var o in r.Skip(secim.Count)) _vm.Salon.CizimOgeleri.Add(o);
                break;
            }
            case SalonCadIslemi.HizalaSol: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.Sol); break;
            case SalonCadIslemi.HizalaMerkezX: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.YatayMerkez); break;
            case SalonCadIslemi.HizalaSag: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.Sag); break;
            case SalonCadIslemi.HizalaAlt: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.Alt); break;
            case SalonCadIslemi.HizalaMerkezY: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.DikeyMerkez); break;
            case SalonCadIslemi.HizalaUst: SalonGelismisCadServisi.Hizala(secim, SalonHizalamaTipi.Ust); break;
            case SalonCadIslemi.DagitYatay: SalonGelismisCadServisi.EsitDagit(secim, true); break;
            case SalonCadIslemi.DagitDikey: SalonGelismisCadServisi.EsitDagit(secim, false); break;
            case SalonCadIslemi.Grupla: SalonGelismisCadServisi.Grupla(secim); break;
            case SalonCadIslemi.GrubuCoz: SalonGelismisCadServisi.GrubuCoz(secim); break;
            case SalonCadIslemi.OfsetliKopya:
            {
                var pano = SalonGelismisCadServisi.PanoyaKopyala(secim);
                foreach (var o in SalonGelismisCadServisi.Yapistir(pano,
                             new Point2D(pano.BazNoktasi.X + istek.A, pano.BazNoktasi.Y + istek.B)))
                    _vm.Salon.CizimOgeleri.Add(o);
                break;
            }
            case SalonCadIslemi.KaliciOlcu:
            {
                var o = secim[^1];
                if (o.Noktalar.Count < 2) throw new InvalidOperationException("Ölçü için en az iki köşeli öğe gerekir.");
                _vm.Salon.Olculendirmeler.Add(new SalonOlculendirme
                {
                    Ad = "Bağlı ölçü", OgeId = o.Id, BaslangicNoktaIndeksi = 0,
                    BitisNoktaIndeksi = o.Noktalar.Count - 1, OfsetM = istek.A
                });
                break;
            }
            case SalonCadIslemi.KisitYatay:
            case SalonCadIslemi.KisitDikey:
            case SalonCadIslemi.KisitUzunluk:
            {
                var o = secim[^1];
                if (o.Noktalar.Count < 2) throw new InvalidOperationException("Kısıt için en az iki köşeli öğe gerekir.");
                var k = new SalonGeometrikKisit
                {
                    Ad = "CAD+ kısıtı", OgeId = o.Id, NoktaIndeksi = 0,
                    IkinciNoktaIndeksi = o.Noktalar.Count - 1,
                    Tip = istek.Islem == SalonCadIslemi.KisitYatay ? SalonKisitTipi.Yatay
                        : istek.Islem == SalonCadIslemi.KisitDikey ? SalonKisitTipi.Dikey
                        : SalonKisitTipi.Uzunluk,
                    Deger = istek.A
                };
                _vm.Salon.GeometrikKisitlar.Add(k);
                var sonuc = SalonKisitServisi.Uygula(_vm.Salon);
                if (!sonuc.Basarili) throw new InvalidOperationException(string.Join(" ", sonuc.Hatalar));
                break;
            }
        }
    }

    private async void AltlikEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Referans Altlık Ekle",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DXF, PDF veya Görsel")
                { Patterns = ["*.dxf", "*.pdf", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.tif", "*.tiff"] }
            ]
        });
        string? path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var altlik = SalonAltlikServisi.DosyaAltligiOlustur(path);
            ModeliDegistir(() => _vm.Salon.ReferansAltliklar.Add(altlik));
            CizimCanvas?.Sigdir();
            _vm.DurumMesaji = altlik.Tip == SalonAltlikTipi.Dxf
                ? $"DXF altlığı eklendi: {altlik.Segmentler.Count} segment."
                : $"{altlik.Tip} altlığı bağlandı; ölçek/dönüşüm bilgisi salon dosyasında korunuyor.";
        }
        catch (Exception ex) { _vm.DurumMesaji = "Altlık eklenemedi: " + ex.Message; }
    }

    private async void Surumler_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SalonSurumlerWindow(_vm.Salon);
        await dialog.ShowDialog(this);
        if (dialog.GeriYuklenenSalon is not { } salon) return;
        if (!await BelgeDegistirmeOnayiAl("Sürümü Geri Yükle")) return;
        BelgeyiUygula(salon, kayitli: false, "Seçilen sürüm geri yüklendi; ana dosya üzerine yazılmadı.", sigdir: true);
    }

    private async void KomutPaleti_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SalonKomutPaletiWindow();
        await dialog.ShowDialog(this);
        if (dialog.SeciliKomut is { } k) await KomutuCalistir(k.Id);
    }

    private Task KomutuCalistir(string id)
    {
        var args = new Avalonia.Interactivity.RoutedEventArgs();
        switch (id)
        {
            case "file.new": Yeni_OnClick(null, args); break;
            case "file.open": Ac_OnClick(null, args); break;
            case "file.save": return Kaydet();
            case "file.saveAs": return FarkliKaydet();
            case "edit.undo": GeriAl(); break;
            case "edit.redo": Yinele(); break;
            case "draw.line": AracSecKisayol(SalonAraci.Cizgi); break;
            case "draw.polyline": AracSecKisayol(SalonAraci.Polyline); break;
            case "draw.rectangle": AracSecKisayol(SalonAraci.Dikdortgen); break;
            case "draw.circle": AracSecKisayol(SalonAraci.Daire); break;
            case "draw.arc": AracSecKisayol(SalonAraci.Yay); break;
            case "modify.move": AracSecKisayol(SalonAraci.Tasi); break;
            case "modify.rotate": AracSecKisayol(SalonAraci.Dondur); break;
            case "modify.mirror": AracSecKisayol(SalonAraci.Aynala); break;
            case "modify.offset": AracSecKisayol(SalonAraci.Offset); break;
            case "modify.trim": AracSecKisayol(SalonAraci.Trim); break;
            case "modify.extend": AracSecKisayol(SalonAraci.Uzat); break;
            case "modify.fillet" or "modify.chamfer" or "modify.array" or "modify.align" or "modify.group":
                GelismisCad_OnClick(null, args); break;
            case "view.fit": CizimCanvas?.Sigdir(); break;
            case "view.split3d": Onizleme3D_OnClick(null, args); break;
            case "view.section" or "tools.seats" or "tools.sightline" or "tools.acoustic" or
                 "tools.quality" or "tools.reflector": Analiz_OnClick(null, args); break;
            case "tools.quickDesign": HizliTasarim_OnClick(null, args); break;
            case "tools.underlay": AltlikEkle_OnClick(null, args); break;
            case "tools.history": Surumler_OnClick(null, args); break;
            default:
                _vm.DurumMesaji = "Komut çekirdek serviste hazır; çoklu öğe/kenar seçimiyle çalıştırılmalıdır: " + id;
                break;
        }
        return Task.CompletedTask;
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

    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await BelgeDegistirmeOnayiAl("Yeni Salon")) return;
        BelgeyiUygula(SalonGeometriServisi.YeniOlustur(), kayitli: true,
            "Yeni salon geometrisi oluşturuldu.", sigdir: false);
    }

    private async void Sablon_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SalonSablonWindow();
        await dialog.ShowDialog(this);
        if (dialog.OlusturulanSalon is not { } yeni) return;
        if (!await BelgeDegistirmeOnayiAl("Hazır Şablon")) return;
        BelgeyiUygula(yeni, kayitli: false, "Şablondan salon oluşturuldu.", sigdir: true);
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
        if (!await BelgeDegistirmeOnayiAl("Salon Aç")) return;
        DosyaAc(path);
    }

    public void DosyaAc(string path)
    {
        try
        {
            var acilan = SalonGeometriServisi.Ac(path);
            BelgeyiUygula(acilan, kayitli: true,
                $"Açıldı: {Path.GetFileName(path)}  (Rev. {acilan.GeometriRevizyon})", sigdir: true);
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Açılamadı: {ex.Message}";
        }
    }

    private void BelgeyiUygula(SalonGeometri salon, bool kayitli, string durum, bool sigdir)
    {
        PanelSenkronize(() =>
        {
            _vm.Salon = salon;
            _vm.SeciliKatman = salon.Katmanlar.FirstOrDefault(k => k.Gorunur)
                               ?? salon.Katmanlar.FirstOrDefault();
        });
        UndoSifirla();
        TumTaslaklariTemizle();
        _kayitliIcerikJson = kayitli ? IcerikJson(salon) : "";
        _vm.DurumMesaji = durum;
        _vm.BasligiYenile();
        PanelSenkronize(SyncPanel);
        BaglaCanvas();
        AracSecKisayol(SalonAraci.Sec);
        DirtyYenile();
        if (sigdir) CizimCanvas?.Sigdir();
        else CizimCanvas?.GorunumuSifirla();
    }

    private async Task<bool> BelgeDegistirmeOnayiAl(string baslik)
    {
        if (!_vm.IsDirty && !_vm.UygulanmamisTaslakVar) return true;
        var secim = await DegisiklikSecimiSor(baslik,
            "Kaydedilmemiş değişiklikler var. Devam etmeden önce ne yapmak istersiniz?");
        return secim switch
        {
            KapanisSecimi.KaydetVeCik => await Kaydet(),
            KapanisSecimi.KaydetmedenCik => true,
            _ => false
        };
    }

    private bool KayitOncesiHazirla()
    {
        var ad = SalonAdiText?.Text ?? "";
        if (!double.TryParse(YukseklikText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double yukseklik)
            || !double.IsFinite(yukseklik) || yukseklik <= 0)
        {
            _vm.DurumMesaji = "Kaydetmeden önce geçerli bir salon yüksekliği girin.";
            YukseklikText?.Focus();
            return false;
        }
        if (!int.TryParse(FasetText?.Text, out int faset) || faset is < 12 or > 128)
        {
            _vm.DurumMesaji = "Kaydetmeden önce faset sayısını 12–128 arasında girin.";
            FasetText?.Focus();
            return false;
        }

        ModeliDegistir(() =>
        {
            _vm.Salon.SalonAdi = ad;
            _vm.Salon.SalonYuksekligiM = yukseklik;
            _vm.Salon.FasetKenarSayisi = faset;
        });
        _vm.BasligiYenile();
        return TaslakKaydaHazirMi();
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await Kaydet();

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydet();

    private async Task<bool> Kaydet()
    {
        if (!KayitOncesiHazirla()) return false;
        if (string.IsNullOrWhiteSpace(_vm.Salon.DosyaYolu))
            return await FarkliKaydet();
        return KaydetYola(_vm.Salon.DosyaYolu);
    }

    private async Task<bool> FarkliKaydet()
    {
        if (!KayitOncesiHazirla()) return false;
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
            _vm.ModelBaglariniYenile();
            KayitBaselineYenile();
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
        ModeliDegistir(() => _vm.Salon.SalonAdi = yeni);
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
                out double v) && double.IsFinite(v) && v > 0)
        {
            if (Math.Abs(v - _vm.SalonYuksekligiM) > 1e-12)
                ModeliDegistir(() => _vm.SalonYuksekligiM = v);
        }
        else if (!string.IsNullOrWhiteSpace(YukseklikText?.Text))
            _vm.DurumMesaji = "Salon yüksekliği sıfırdan büyük geçerli bir sayı olmalı.";
        if (YukseklikText is { } tb) tb.Text = _vm.SalonYuksekligiM.ToString("F2");
    }

    // ── Katman ────────────────────────────────────────────────────────────────

    private void KatmanEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var katman = SalonKatman.Olustur(SalonKatmanTipi.Genel);
        katman.Ad = $"Katman {_vm.Salon.Katmanlar.Count + 1}";
        ModeliDegistir(() => _vm.Salon.Katmanlar.Add(katman));
        _vm.SeciliKatman = katman;
        _vm.DurumMesaji = $"'{katman.Ad}' katmanı eklendi.";
    }

    // ── Navigasyon / kapanış ──────────────────────────────────────────────────

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (CizimCanvas is not { } c) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool metinOdakli = e.Source is TextBox;
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z when !metinOdakli && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Yinele(); e.Handled = true; return;
                case Key.Z when !metinOdakli:
                    GeriAl(); e.Handled = true; return;
                case Key.Y when !metinOdakli:
                    Yinele(); e.Handled = true; return;
                case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    _ = FarkliKaydet(); e.Handled = true; return;
                case Key.S:
                    _ = Kaydet(); e.Handled = true; return;
                case Key.N:
                    Yeni_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs()); e.Handled = true; return;
                case Key.O:
                    Ac_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs()); e.Handled = true; return;
                case Key.K:
                    KomutPaleti_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs()); e.Handled = true; return;
            }
            return; // Ctrl/Meta kombinasyonları tek-harf araç kısayoluna düşmez.
        }

        if (metinOdakli) return;

        if (!c.IsFocused)
        {
            if (e.Key == Key.Delete)
            {
                var args = new Avalonia.Interactivity.RoutedEventArgs();
                if (SeyirciList?.IsKeyboardFocusWithin == true) SeyirciSil_OnClick(null, args);
                else if (BalkonList?.IsKeyboardFocusWithin == true) BalkonSil_OnClick(null, args);
                else if (AciklikList?.IsKeyboardFocusWithin == true) AciklikSil_OnClick(null, args);
                else if (TavanList?.IsKeyboardFocusWithin == true) TavanNoktaSil_OnClick(null, args);
                else if (KatmanList?.IsKeyboardFocusWithin == true) KatmanSil_OnClick(null, args);
                else return;
                e.Handled = true;
            }
            return;
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
            case Key.Delete:
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
        EditorAyarlariniKaydet();
        _otomatikKayitZamanlayici.Stop();
        _kapanisOnaylandi = true;
        Close();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;
        e.Cancel = true;
        if (!await KapanisOnayiAl()) return;
        EditorAyarlariniKaydet();
        _otomatikKayitZamanlayici.Stop();
        _kapanisOnaylandi = true;
        Close();
    }

    private enum KapanisSecimi { Iptal, KaydetVeCik, KaydetmedenCik }

    private async Task<bool> KapanisOnayiAl()
    {
        if (!_vm.IsDirty && !_vm.UygulanmamisTaslakVar) return true;
        var secim = await DegisiklikSecimiSor("Modülü Kapat",
            "Kaydedilmemiş değişiklikler var. Modülü kapatmadan önce ne yapmak istersiniz?");
        return secim switch
        {
            KapanisSecimi.KaydetVeCik => await Kaydet(),
            KapanisSecimi.KaydetmedenCik => true,
            _ => false
        };
    }

    private async Task<KapanisSecimi> DegisiklikSecimiSor(string baslik, string mesaj)
    {
        var dialog = new Window
        {
            Title = baslik,
            Width = 490, Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var sonuc = KapanisSecimi.Iptal;
        var kaydet = DialogButton("Kaydet", "#0E5B82", "#1A8BBF", Avalonia.Media.Brushes.White, 110);
        var kaydetmeden = DialogButton("Kaydetmeden Devam", "#3A1010", "#7A2020", Avalonia.Media.Brush.Parse("#F0B0B0"), 175);
        var iptal = DialogButton("İptal", "#0D2535", "#1A5070", Avalonia.Media.Brush.Parse("#C8E4F4"), 90);
        kaydet.IsDefault = true;
        iptal.IsCancel = true;

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
                    Text = mesaj,
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
        evet.IsDefault = true;
        hayir.IsCancel = true;
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
