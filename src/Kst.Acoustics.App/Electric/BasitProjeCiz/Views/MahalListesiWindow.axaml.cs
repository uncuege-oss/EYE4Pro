using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class MahalListesiWindow : Window, INotifyPropertyChanged
{
    private readonly BasitProjeCizViewModel _vm;
    private bool _zayif;          // false = kuvvetli, true = zayıf akım
    private bool _yukleniyor;     // ComboBox event'lerinde tekrar kurmayı önle
    /// Yerleşim ön-tahmini için aday üretici (yerleştirmenin kullandığı motorun aynısı).
    private readonly BasitProjeCizGeometriServisi _geometri = new();

    // Disipline göre seçilebilir malzeme tipleri
    // DİKKAT: Kütüphanede sembolü olan (TipBlokAdindan'ın dönebildiği) her kuvvetli
    // akım tipi bu dizide OLMAK ZORUNDA. Eksik tip, kalem-ekle dropdown'ında görünür
    // ama satır tabloda filtrelenir → "ekledim, listeden kayboldu" hatası oluşur
    // (Sensör için codex12 §5'te belgelenen hata sınıfı).
    private static readonly BpcElemanTipi[] KuvvetliTipler =
    {
        BpcElemanTipi.Armatür, BpcElemanTipi.SarkitArmatur, BpcElemanTipi.GlopArmatur,
        BpcElemanTipi.CiplakLamba, BpcElemanTipi.BantArmatur, BpcElemanTipi.Avize,
        BpcElemanTipi.WallWasher,
        BpcElemanTipi.Anahtar, BpcElemanTipi.Komütatör, BpcElemanTipi.UcluKomutator,
        BpcElemanTipi.DarbeAnahtari, BpcElemanTipi.Vavien, BpcElemanTipi.KomütatörVavien,
        BpcElemanTipi.Deviatör, BpcElemanTipi.Dimmer, BpcElemanTipi.IkiKutupluAnahtar,
        BpcElemanTipi.Sensör,
        BpcElemanTipi.Priz, BpcElemanTipi.KapakliPriz, BpcElemanTipi.SivaUstuPriz,
        BpcElemanTipi.TrifazePriz, BpcElemanTipi.KombinePrizKutusu, BpcElemanTipi.TavanPrizi,
        BpcElemanTipi.UsbliMasaPrizi, BpcElemanTipi.UpsPrizi, BpcElemanTipi.KapakliUpsPrizi,
        BpcElemanTipi.SivaUstuUpsPrizi, BpcElemanTipi.IkiliPriz,
        BpcElemanTipi.UcluPriz, BpcElemanTipi.Buat, BpcElemanTipi.KatPanosu,
        BpcElemanTipi.KlimaIcUnite, BpcElemanTipi.KlimaDisUnite
    };

    private static readonly BpcElemanTipi[] ZayifTipler =
    {
        BpcElemanTipi.DataPriz, BpcElemanTipi.TvPriz, BpcElemanTipi.TelefonPriz,
        BpcElemanTipi.Kamera, BpcElemanTipi.CctvKayit, BpcElemanTipi.AgAnahtari,
        BpcElemanTipi.Hoparlor, BpcElemanTipi.AnonsAmfi,
        BpcElemanTipi.Diafon, BpcElemanTipi.KapiZili, BpcElemanTipi.DiafonSantrali,
        BpcElemanTipi.DumanDedektor, BpcElemanTipi.IsiDedektor, BpcElemanTipi.KombineDedektor,
        BpcElemanTipi.YanginButonu, BpcElemanTipi.Siren, BpcElemanTipi.Flasor,
        BpcElemanTipi.YanginSantrali, BpcElemanTipi.DataRack, BpcElemanTipi.KatDagitimKutusu,
        BpcElemanTipi.TvMerkezi, BpcElemanTipi.TelefonDagitim
    };

    public string[] YerlesimTipleri { get; } = { "Adet", "Grid", "Duvar aralıklı", "Açıklık Yanı", "Homojen Duvar" };
    public string[] MontajTipleri { get; } = { "Tavan", "Duvar", "Zemin" };

    private BpcMahalSatiri? _seciliSatir;
    private bool _tabloGuncelleniyor;

    // Sürükle-boya durumu: basılı tutup hücreler üzerinden geçince ilk hücrenin
    // yeni durumu (aktif/pasif) geçilen hücrelere uygulanır.
    private bool _boyaAktif;
    private bool _boyaDegeri;

    // Çift yönlü vurgu (M5): canvas'ta oda seçilince mahal listesinde ilgili
    // sütun turkuaz çerçeveyle işaretlenir ve görünüme kaydırılır.
    private string? _canvasVurguOdaId;
    private const double OdaSutunGenislik = 46.0;

    // Seçili satır/hücrenin GERÇEK değişikliklerini (INPC eşitlik korumalı)
    // IsDirty'ye bağlar — panel rebind'ları yanlış pozitif üretmez.
    private void SatirVeyaHucreDegisti(object? sender, PropertyChangedEventArgs e) => _vm.Kirlet();

    public BpcMahalSatiri? SeciliSatir
    {
        get => _seciliSatir;
        set
        {
            if (_seciliSatir != null) _seciliSatir.PropertyChanged -= SatirVeyaHucreDegisti;
            _seciliSatir = value;
            if (_seciliSatir != null) _seciliSatir.PropertyChanged += SatirVeyaHucreDegisti;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SeciliSatirAktif));
            OnPropertyChanged(nameof(GenelAyarPaneliAktif));
            OnPropertyChanged(nameof(OdaOzelPaneliAktif));
            OnPropertyChanged(nameof(EfektifAdetModu));
            BildirHucreOzellikGorunurluk();
            if (!_tabloGuncelleniyor) TabloKur(); // Re-render to show selection highlight
        }
    }
    public bool SeciliSatirAktif => _seciliSatir != null;

    private BpcMahalHucreAyar? _seciliHucreAyar;
    public BpcMahalHucreAyar? SeciliHucreAyar
    {
        get => _seciliHucreAyar;
        set
        {
            if (_seciliHucreAyar != null) _seciliHucreAyar.PropertyChanged -= SatirVeyaHucreDegisti;
            _seciliHucreAyar = value;
            if (_seciliHucreAyar != null) _seciliHucreAyar.PropertyChanged += SatirVeyaHucreDegisti;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGenelAyarModu));
            OnPropertyChanged(nameof(GenelAyarPaneliAktif));
            OnPropertyChanged(nameof(OdaOzelPaneliAktif));
            OnPropertyChanged(nameof(EfektifAdetModu));
            BildirHucreOzellikGorunurluk();
        }
    }

    // ── Oda-özel eleman özellikleri görünürlüğü (seçili satırın sembol tipine göre) ──
    private BpcArmatürTanim? SeciliSatirTanim
    {
        get
        {
            var satir = _seciliSatir;
            if (satir == null) return null;
            var id = satir.ArmatürTanimId;
            var byId = string.IsNullOrEmpty(id) ? null
                : _vm.Armatürler.Concat(_vm.Anahtarlar).Concat(_vm.Prizler).Concat(_vm.Klimalar)
                      .FirstOrDefault(t => t.Id == id);
            return byId ?? BasitProjeCizKutuphaneServisi.ZayifAkimTanim(satir.Tip);
        }
    }
    private string SeciliSatirOzelAlan => SeciliSatirTanim?.OzelAlanTuru ?? "";

    public bool GosterGenelAyarWatt => GenelAyarPaneliAktif &&
        ((SeciliSatirTanim?.GucW ?? 0) > 0 || SeciliSatirOzelAlan == "OzelArmatur");

    public bool GosterHucreWatt => OdaOzelPaneliAktif &&
        ((SeciliSatirTanim?.GucW ?? 0) > 0 || SeciliSatirOzelAlan == "OzelArmatur" || SeciliSatirOzelAlan == "Adet");
    public bool GosterHucreKombinePriz => OdaOzelPaneliAktif && SeciliSatirOzelAlan == "KombinePrizKutusu";
    public bool GosterHucreMegapiksel  => OdaOzelPaneliAktif && SeciliSatirOzelAlan == "Megapiksel";
    public bool GosterHucreCihazAdet   => OdaOzelPaneliAktif && SeciliSatirOzelAlan == "Adet";
    // Sensör seçeneği yalnız armatür satırlarında, oda-özel modda görünür.
    public bool GosterHucreSensor => OdaOzelPaneliAktif && _seciliSatir != null &&
        BasitProjeCizKabloServisi.ElemanArmaturMi(_seciliSatir.Tip);

    /// Seçili hücrenin sensör işareti (checkbox iki yönlü bağlanır).
    public bool SeciliHucreSensorlu
    {
        get => _seciliHucreAyar?.Sensorlu ?? false;
        set
        {
            if (_seciliHucreAyar == null || _seciliHucreAyar.Sensorlu == value) return;
            if (KilitliyseDur()) { OnPropertyChanged(); return; }
            _seciliHucreAyar.Sensorlu = value;
            OnPropertyChanged();
        }
    }

    private void BildirHucreOzellikGorunurluk()
    {
        OnPropertyChanged(nameof(GosterGenelAyarWatt));
        OnPropertyChanged(nameof(GosterHucreWatt));
        OnPropertyChanged(nameof(GosterHucreKombinePriz));
        OnPropertyChanged(nameof(GosterHucreMegapiksel));
        OnPropertyChanged(nameof(GosterHucreCihazAdet));
        OnPropertyChanged(nameof(GosterHucreSensor));
        OnPropertyChanged(nameof(SeciliHucreSensorlu));
    }
    
    private string? _seciliOdaAd;
    public string? SeciliOdaAd
    {
        get => _seciliOdaAd;
        set
        {
            _seciliOdaAd = value;
            OnPropertyChanged();
        }
    }

    public bool IsGenelAyarModu => _seciliHucreAyar == null;
    public bool GenelAyarPaneliAktif => _seciliSatir != null && _seciliHucreAyar == null;
    public bool OdaOzelPaneliAktif => _seciliSatir != null && _seciliHucreAyar != null;
    public bool MahalDuzenlemeAktif => !_vm.ProjeKilitli;

    public bool EfektifAdetModu
    {
        get
        {
            if (_seciliSatir == null || _seciliHucreAyar == null) return false;
            string tip = _seciliHucreAyar.GenelAyarKullan
                ? _seciliSatir.YerlesimTipi
                : _seciliHucreAyar.YerlesimTipi;
            if (tip == "Adet") return true;
            // Priz satırlarında "Duvar aralıklı", "Açıklık Yanı" ve "Homojen Duvar"
            // da adet girişi zorunludur.
            bool isPriz = BasitProjeCizKabloServisi.ElemanPrizMi(_seciliSatir.Tip);
            return isPriz && tip is "Duvar aralıklı" or "Açıklık Yanı" or "Homojen Duvar";
        }
    }

    public MahalListesiWindow()
    {
        _vm = new BasitProjeCizViewModel();
        InitializeComponent();
        DataContext = this;
    }

    public MahalListesiWindow(BasitProjeCizViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = this;
        // M12/M16 kabuk ayrımı: pencere modülün disiplinine sabitlenir.
        _zayif = vm.ModulZayifMi;
        KuvvetliBtn.IsChecked = !_zayif;
        ZayifBtn.IsChecked = _zayif;
        // Tek disiplinli kabukta disiplin geçiş sekmeleri gizlenir.
        KuvvetliBtn.IsVisible = ZayifBtn.IsVisible =
            DisiplinAyirici1.IsVisible = DisiplinAyirici2.IsVisible = false;
        KatComboDoldur();
        KalemComboDoldur();
        TabloKur();
        // "Sadece eksikleri ekle" işareti VM ile iki yönlü senkron: ana penceredeki
        // yerleştir butonu da aynı değeri kullandığı için iki giriş noktası aynı
        // modda çalışır (gizli mod farkı oluşmaz).
        SadecaEksikleriCheck.IsChecked = _vm.SadecaEksikleriEkle;
        SadecaEksikleriCheck.IsCheckedChanged += (_, _) =>
            _vm.SadecaEksikleriEkle = SadecaEksikleriCheck.IsChecked == true;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BasitProjeCizViewModel.ProjeKilitli))
            {
                OnPropertyChanged(nameof(MahalDuzenlemeAktif));
                TabloKur();
            }
            else if (e.PropertyName == nameof(BasitProjeCizViewModel.Elemanlar))
            {
                // Eleman undo/redo sonrası tabloyu yenile
                Avalonia.Threading.Dispatcher.UIThread.Post(() => TabloKur());
            }
        };

        KatCombo.SelectionChanged += (_, _) => { if (!_yukleniyor) { SeciliSatir = null; KalemComboDoldur(); TabloKur(); } };
        Closed += (_, _) => _vm.MahalListesiOdaVurgula(null); // pencere kapanınca vurguyu kaldır

        // Sürükle-boya her yerde biter (hücre dışında bırakılsa bile takılı kalmasın).
        AddHandler(PointerReleasedEvent, (_, _) =>
        {
            if (!_boyaAktif) return;
            _boyaAktif = false;
            TabloKur(); // toplam sütunu + tüm hücre görselleri tazelensin
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        KeyDown += MahalListesi_KeyDown;

        // Çift yönlü vurgu: canvas'ta oda seçilince (SeciliOda değişince) bu
        // pencerede ilgili sütun vurgulanır ve görünüme kaydırılır.
        _vm.PropertyChanged += MahalListesi_VmSeciliOdaChanged;
        Closed += (_, _) => _vm.PropertyChanged -= MahalListesi_VmSeciliOdaChanged;
    }

    private void MahalListesi_VmSeciliOdaChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BasitProjeCizViewModel.SeciliOda)) return;
        var odaId = _vm.SeciliOda?.Id;
        if (odaId == _canvasVurguOdaId) return;
        _canvasVurguOdaId = odaId;

        var kat = SeciliKat;
        if (kat == null || odaId == null) { TabloKur(); return; }

        var odalar = GosterilecekOdalar(kat);
        int idx = odalar.FindIndex(o => o.Id == odaId);
        TabloKur();
        if (idx >= 0) TabloYatayScroll.Offset = new Avalonia.Vector(Math.Max(0, idx * OdaSutunGenislik - 80), 0);
    }

    // ── Klavye gezinme (M4) ─────────────────────────────────────────────────
    // Ok tuşları hücre gezdirir, Space aktif/pasif, rakam Adet'e yazar,
    // Delete hücreyi pasifleştirir. Metin kutularında devreye girmez.
    private void MahalListesi_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown or ComboBox) return;
        var kat = SeciliKat;
        if (kat == null || _seciliSatir == null) return;

        var satirlar = kat.MahalListesi.Where(s => AktifTipler.Contains(s.Tip)).ToList();
        var odalar = GosterilecekOdalar(kat);
        if (satirlar.Count == 0 || odalar.Count == 0) return;

        int satirIdx = satirlar.IndexOf(_seciliSatir);
        if (satirIdx < 0) return;
        int odaIdx = _seciliHucreAyar == null
            ? -1
            : odalar.FindIndex(o => ReferenceEquals(_seciliSatir.HucreGetirVeyaOlustur(o.Id), _seciliHucreAyar));

        void HucreSec(int yeniSatir, int yeniOda)
        {
            yeniSatir = Math.Clamp(yeniSatir, 0, satirlar.Count - 1);
            yeniOda = Math.Clamp(yeniOda, 0, odalar.Count - 1);
            var s = satirlar[yeniSatir];
            var o = odalar[yeniOda];
            SeciliOdaAd = o.Ad;
            SeciliHucreAyar = s.HucreGetirVeyaOlustur(o.Id);
            _tabloGuncelleniyor = true;
            SeciliSatir = s;
            _tabloGuncelleniyor = false;
            _vm.MahalListesiOdaVurgula(o.Id);
            OnPropertyChanged(nameof(EfektifAdetModu));
            GorselleriGuncelle();
        }

        switch (e.Key)
        {
            case Key.Right: HucreSec(satirIdx, odaIdx + 1); e.Handled = true; return;
            case Key.Left:  HucreSec(satirIdx, Math.Max(0, odaIdx - 1)); e.Handled = true; return;
            case Key.Down:  HucreSec(satirIdx + 1, Math.Max(0, odaIdx)); e.Handled = true; return;
            case Key.Up:    HucreSec(satirIdx - 1, Math.Max(0, odaIdx)); e.Handled = true; return;
        }

        if (_seciliHucreAyar == null || odaIdx < 0) return;
        var aktifOda = odalar[odaIdx];
        if (!aktifOda.MahalListesineDahil) return;

        if (e.Key == Key.Space)
        {
            if (KilitliyseDur()) { e.Handled = true; return; }
            _seciliHucreAyar.Aktif = !_seciliHucreAyar.Aktif;
            if (_seciliHucreAyar.Aktif)
            {
                var (yer, _, _, _) = _seciliSatir.EfektifAyarlar(aktifOda.Id);
                if (yer == "Adet" && _seciliHucreAyar.Adet == 0) _seciliHucreAyar.Adet = 1;
            }
            _vm.Kirlet();
            TabloKur();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (KilitliyseDur()) { e.Handled = true; return; }
            _seciliHucreAyar.Aktif = false; // Adet korunur (§5 kuralı)
            _vm.Kirlet();
            TabloKur();
            e.Handled = true;
            return;
        }

        // Rakam tuşu: doğrudan adet yaz (Adet modundaki hücrelerde).
        int? rakam = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => e.Key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => null
        };
        if (rakam is { } n && EfektifAdetModu)
        {
            if (KilitliyseDur()) { e.Handled = true; return; }
            _seciliHucreAyar.Adet = n;
            _seciliHucreAyar.Aktif = n > 0;
            _vm.Kirlet();
            TabloKur();
            e.Handled = true;
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // NOT: Tablo düzenlemeleri (hücre tıklama, adet, grid, montaj vb.) artık anında
    // yerleştirme TETİKLEMEZ. Yalnız mahal listesi durumunu ve tablo görselini günceller.
    // Asıl yerleşim yalnız "Otomatik Yerleştir" butonuyla yapılır; böylece her tıklamada
    // elemanlar silinip yeniden üretilmez ve mevcut armatürler kaybolmaz.

    private void AyarCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(EfektifAdetModu));
        TabloKur();
    }

    private void AyarCheckbox_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnPropertyChanged(nameof(EfektifAdetModu));
        TabloKur();
    }

    private void AdetDegisti(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_seciliHucreAyar == null || _yukleniyor) return;
        if (KilitliyseDur()) return;
        int n = (int)(e.NewValue ?? 0);
        _seciliHucreAyar.Adet = n;
        _seciliHucreAyar.Aktif = n > 0;
        GorselleriGuncelle();
    }

    private void GridAlanLostFocus(object? sender, RoutedEventArgs e)
    {
        // Grid/duvar aralığı yalnız modele yazılır (TwoWay binding); yerleşim
        // butona basılınca uygulanır. Tablo görseli değişmediği için işlem yok.
    }

    private void GelenGucWDegisti(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_yukleniyor || SeciliSatir == null) return;
        if (KilitliyseDur()) return;
        double? yeniDeger = e.NewValue is > 0 ? (double?)e.NewValue : null;
        SeciliSatir.GelenGucW = yeniDeger;
    }

    private BasitProjeKat? SeciliKat => KatCombo.SelectedItem as BasitProjeKat;

    private void KatComboDoldur()
    {
        _yukleniyor = true;
        KatCombo.ItemsSource = _vm.KatlarTersSira.ToList();
        KatCombo.SelectedItem = _vm.SeciliKat ?? _vm.Katlar.FirstOrDefault();
        _yukleniyor = false;
    }

    private void Disiplin_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _zayif = ReferenceEquals(sender, ZayifBtn);
        KuvvetliBtn.IsChecked = !_zayif;
        ZayifBtn.IsChecked = _zayif;
        // Disiplin değişince diğer disiplinin satırına ait stale seçim panelini temizle
        SeciliHucreAyar = null;
        SeciliOdaAd = null;
        SeciliSatir = null;
        KalemComboDoldur();
        TabloKur();
    }

    private BpcElemanTipi[] AktifTipler => _zayif ? ZayifTipler : KuvvetliTipler;

    private void KalemComboDoldur()
    {
        var kat = SeciliKat;
        var mevcutBloklar = kat?.MahalListesi
            .Select(s => s.ArmatürTanimId ?? s.Tip.ToString())
            .ToHashSet() ?? new HashSet<string>();

        var semboller = SembolListesiGetir();
        var eklenebilir = semboller
            .Where(t => !mevcutBloklar.Contains(t.Id) && !mevcutBloklar.Contains(t.Tip.ToString()))
            .ToList();

        KalemCombo.ItemsSource = eklenebilir.Select(t => $"{t.BlokAdi}  —  {t.Ad}").ToList();
        KalemCombo.Tag = eklenebilir;
        if (eklenebilir.Count > 0) KalemCombo.SelectedIndex = 0;
    }

    private List<(string Id, BpcElemanTipi Tip, string Ad, string BlokAdi, BpcArmatürTanim Tanim)> SembolListesiGetir()
    {
        var sonuc = new List<(string, BpcElemanTipi, string, string, BpcArmatürTanim)>();

        if (_zayif)
        {
            foreach (var tip in ZayifTipler)
            {
                var tanim = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip);
                if (tanim is not null)
                    sonuc.Add((tanim.BlokAdi, tip, tanim.Ad, tanim.BlokAdi, tanim));
            }
        }
        else
        {
            var tipSira = new[] {
                _vm.Armatürler, _vm.Anahtarlar, _vm.Prizler, _vm.Klimalar
            };
            foreach (var liste in tipSira)
            {
                foreach (var tanim in liste)
                {
                    var tip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(tanim.BlokAdi) ?? BpcElemanTipi.Armatür;
                    sonuc.Add((tanim.Id, tip, tanim.Ad, tanim.BlokAdi, tanim));
                }
            }
        }

        return sonuc;
    }

    private void KalemEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        var kat = SeciliKat;
        if (kat == null) return;
        if (KalemCombo.Tag is not List<(string Id, BpcElemanTipi Tip, string Ad, string BlokAdi, BpcArmatürTanim Tanim)> liste) return;
        int idx = KalemCombo.SelectedIndex;
        if (idx < 0 || idx >= liste.Count) return;

        var (id, tip, ad, blokAdi, tanim) = liste[idx];
        bool isPriz = BasitProjeCizKabloServisi.ElemanPrizMi(tip);
        _vm.Kirlet();
        kat.MahalListesi.Add(new BpcMahalSatiri
        {
            Tip            = tip,
            ArmatürTanimId = id,
            ArmatürTanimAd = $"{blokAdi} — {ad}",
            // Priz tipler için varsayılan: duvar montajı, kullanıcı adet girer
            MontajTipi  = tanim.MontajTipi,
            YerlesimTipi = isPriz ? "Açıklık Yanı" : "Adet"
        });
        KalemComboDoldur();
        TabloKur();
    }

    // ── Matris tabloyu kur (sütun = oda, satır = malzeme, hücre = adet) ──────
    private void TabloKur()
    {
        var kat = SeciliKat;
        if (kat == null)
        {
            TabloHostSol.Child = new TextBlock { Text = "Kat seçin.", Foreground = Brushes.Gray };
            TabloHostSag.Child = null;
            return;
        }

        var odalar = GosterilecekOdalar(kat);
        var satirlar = kat.MahalListesi.Where(s => AktifTipler.Contains(s.Tip)).ToList();

        // Undo/redo mahal listesini yeni nesnelerle değiştirmiş olabilir — bayat
        // satır/hücre seçimi panelde hayalet nesneyi düzenlemesin.
        if (_seciliSatir != null && !satirlar.Contains(_seciliSatir))
        {
            _seciliSatir = null;
            _seciliHucreAyar = null;
            _seciliOdaAd = null;
            OnPropertyChanged(nameof(SeciliSatir));
            OnPropertyChanged(nameof(SeciliHucreAyar));
            OnPropertyChanged(nameof(SeciliOdaAd));
            OnPropertyChanged(nameof(SeciliSatirAktif));
            OnPropertyChanged(nameof(GenelAyarPaneliAktif));
            OnPropertyChanged(nameof(OdaOzelPaneliAktif));
            BildirHucreOzellikGorunurluk();
        }

        if (odalar.Count == 0)
        {
            TabloHostSol.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_odaFiltre)
                    ? "Bu katta oda yok."
                    : $"\"{_odaFiltre}\" ile eşleşen oda yok — filtreyi temizleyin.",
                Foreground = Brushes.Gray
            };
            TabloHostSag.Child = null;
            return;
        }

        // Başlık satır yüksekliğini en uzun oda adına göre tahmin et (90° döndürülmüş metin)
        double baslikYuk = odalar.Max(o =>
        {
            var ad = string.IsNullOrWhiteSpace(o.Ad) ? "Oda" : o.Ad;
            return ad.Length * 7.5 + 30;
        });
        baslikYuk = Math.Max(80, Math.Min(baslikYuk, 280));
        const double veriYuk = 26.0;

        // ── Sol grid: [sil] [malzeme adı] [toplam] — sabit, kaymaz ───────
        var gridSol = new Grid();
        gridSol.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        gridSol.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        gridSol.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        gridSol.RowDefinitions.Add(new RowDefinition(new GridLength(baslikYuk)));
        foreach (var _ in satirlar)
            gridSol.RowDefinitions.Add(new RowDefinition(new GridLength(veriYuk)));

        gridSol.Children.Add(BaslikHucre("Malzeme \\ Oda", 0, 0, 2));
        gridSol.Children.Add(BaslikHucre("Toplam", 0, 2, 1));

        // ── Sağ grid: [her oda için 46px sütun] — yatay kayar ───
        var gridSag = new Grid();
        foreach (var _ in odalar)
            gridSag.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        gridSag.RowDefinitions.Add(new RowDefinition(new GridLength(baslikYuk)));
        foreach (var _ in satirlar)
            gridSag.RowDefinitions.Add(new RowDefinition(new GridLength(veriYuk)));

        // Oda başlık sütunları → gridSag satır 0
        for (int c = 0; c < odalar.Count; c++)
        {
            var oda = odalar[c];
            var dahil = oda.MahalListesineDahil;

            var tb = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(oda.Ad) ? "Oda" : oda.Ad,
                Foreground = new SolidColorBrush(Color.Parse(dahil ? "#78A060" : "#555555")),
                FontSize = 11, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextDecorations = dahil ? null : Avalonia.Media.TextDecorations.Strikethrough
            };

            var iconTb = new TextBlock
            {
                Text = dahil ? "✓ " : "✕ ",
                Foreground = new SolidColorBrush(Color.Parse(dahil ? "#78A060" : "#884444")),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { iconTb, tb }
            };

            var ltc = new LayoutTransformControl
            {
                LayoutTransform = new RotateTransform(-90),
                Child = sp,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Canvas'ta seçilen oda (çift yönlü vurgu): sütun başlığı turkuaz çerçeveyle işaretlenir.
            bool canvasVurgulu = oda.Id == _canvasVurguOdaId;
            var border = new Border
            {
                Tag = "odacol",
                Name = null,
                Background = new SolidColorBrush(Color.Parse(canvasVurgulu ? "#0E2A2A" : (dahil ? "#0E2211" : "#1A1A1A"))),
                BorderBrush = new SolidColorBrush(Color.Parse(canvasVurgulu ? "#40E0D0" : (dahil ? "#1A3A1A" : "#333333"))),
                BorderThickness = new Avalonia.Thickness(canvasVurgulu ? 1.5 : 0.5),
                Padding = new Avalonia.Thickness(2, 6),
                Child = ltc,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            var yaklananOda = oda;
            var yakSatirlar = satirlar;
            border.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _vm.MahalListesiOdaVurgula(yaklananOda.Id);
                if (KilitliyseDur()) return;

                // Çift tık: bu ODADAKİ tüm kalemleri topluca aç/kapat.
                if (e.ClickCount == 2)
                {
                    bool hepsiAktif = yakSatirlar.Count > 0 &&
                        yakSatirlar.All(s => s.HucreGetirVeyaOlustur(yaklananOda.Id).Aktif);
                    foreach (var s in yakSatirlar)
                    {
                        var a = s.HucreGetirVeyaOlustur(yaklananOda.Id);
                        a.Aktif = !hepsiAktif;
                        if (a.Aktif)
                        {
                            var (yer, _, _, _) = s.EfektifAyarlar(yaklananOda.Id);
                            if (yer == "Adet" && a.Adet == 0) a.Adet = 1;
                        }
                    }
                    _vm.Kirlet();
                    TabloKur();
                    _vm.DurumMesaji = hepsiAktif
                        ? $"{yaklananOda.Ad}: tüm kalemler kapatıldı."
                        : $"{yaklananOda.Ad}: tüm kalemler açıldı.";
                    return;
                }

                // Tek tık: odayı mahal listesine dahil et / hariç tut (mevcut davranış)
                yaklananOda.MahalListesineDahil = !yaklananOda.MahalListesineDahil;
                _vm.Kirlet();
                TabloKur();
            };

            Grid.SetRow(border, 0);
            Grid.SetColumn(border, c);      // ← c (gridSag'da 0'dan başlar)
            gridSag.Children.Add(border);
        }

        // Malzeme satırları
        for (int r = 0; r < satirlar.Count; r++)
        {
            var satir = satirlar[r];

            // Sil → gridSol col 0
            var sil = new Button
            {
                Content = "✕", FontSize = 11, Height = 22, Width = 24, Padding = new Avalonia.Thickness(0),
                Background = new SolidColorBrush(Color.Parse("#3A1A1A")),
                Foreground = new SolidColorBrush(Color.Parse("#FF8888")),
                BorderBrush = new SolidColorBrush(Color.Parse("#552222")),
                BorderThickness = new Avalonia.Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var yakalananSatir = satir;
            sil.Click += (_, _) =>
            {
                if (KilitliyseDur()) return;
                if (ReferenceEquals(SeciliSatir, yakalananSatir))
                {
                    SeciliSatir = null;
                    SeciliHucreAyar = null;
                }
                _vm.MahalSatiriKaldir(kat, yakalananSatir);
                KalemComboDoldur();
                TabloKur();
            };
            Grid.SetRow(sil, r + 1);
            Grid.SetColumn(sil, 0);
            gridSol.Children.Add(sil);

            // Malzeme adı → gridSol col 1
            string satirAd = !string.IsNullOrWhiteSpace(satir.ArmatürTanimAd)
                ? satir.ArmatürTanimAd
                : TipAd(satir.Tip);

            var adHucre = AdHucre(satirAd, r + 1, satir == SeciliSatir && SeciliHucreAyar == null);
            var yakOdalar = odalar;
            adHucre.PointerPressed += (_, e) => {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

                // Çift tık: bu KALEMİ tüm (dahil) odalarda topluca aç/kapat.
                if (e.ClickCount == 2)
                {
                    if (KilitliyseDur()) return;
                    var hedefOdalar = yakOdalar.Where(o => o.MahalListesineDahil).ToList();
                    bool hepsiAktif = hedefOdalar.Count > 0 &&
                        hedefOdalar.All(o => satir.HucreGetirVeyaOlustur(o.Id).Aktif);
                    foreach (var o in hedefOdalar)
                    {
                        var a = satir.HucreGetirVeyaOlustur(o.Id);
                        a.Aktif = !hepsiAktif;
                        if (a.Aktif)
                        {
                            var (yer, _, _, _) = satir.EfektifAyarlar(o.Id);
                            if (yer == "Adet" && a.Adet == 0) a.Adet = 1;
                        }
                    }
                    _vm.Kirlet();
                    TabloKur();
                    _vm.DurumMesaji = hepsiAktif
                        ? $"{satirAd}: tüm odalarda kapatıldı."
                        : $"{satirAd}: tüm odalarda açıldı ({hedefOdalar.Count} oda).";
                    return;
                }

                SeciliHucreAyar = null;
                SeciliOdaAd = null;
                _vm.MahalListesiOdaVurgula(null);
                _tabloGuncelleniyor = true;
                SeciliSatir = satir;
                _tabloGuncelleniyor = false;
                GorselleriGuncelle();
            };
            ToolTip.SetTip(adHucre, "Tek tık: satırı seç · Çift tık: bu kalemi tüm odalarda aç/kapat");
            gridSol.Children.Add(adHucre);

            // Toplam sütunu → gridSol col 2 (aktif oda sayısı + toplam adet)
            var toplamHucre = new Border
            {
                Tag = "totalcell",
                Background = new SolidColorBrush(Color.Parse("#081408")),
                BorderBrush = new SolidColorBrush(Color.Parse("#1A3A1A")),
                BorderThickness = new Avalonia.Thickness(0.5),
                Padding = new Avalonia.Thickness(6, 4),
                Child = new TextBlock
                {
                    Text = SatirToplamMetni(satir, odalar),
                    Foreground = new SolidColorBrush(Color.Parse("#78A060")),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetRow(toplamHucre, r + 1);
            Grid.SetColumn(toplamHucre, 2);
            gridSol.Children.Add(toplamHucre);

            // Veri hücreleri → gridSag
            for (int c = 0; c < odalar.Count; c++)
            {
                var oda = odalar[c];
                var ayar = satir.HucreGetirVeyaOlustur(oda.Id);

                var (efektifYerlesim, _, _, _) = satir.EfektifAyarlar(oda.Id);
                bool isAdet = efektifYerlesim == "Adet";

                bool isCellSelected = (satir == SeciliSatir && ayar == SeciliHucreAyar);
                bool odaDahil = oda.MahalListesineDahil;
                bool efektifAktif = satir.EfektifAktif(oda.Id);

                string cellText;
                if (!odaDahil)
                    cellText = "✕";
                else if (efektifAktif)
                    cellText = isAdet ? (ayar.Adet > 0 ? ayar.Adet.ToString(CultureInfo.InvariantCulture) : "✓") : "✓";
                else
                    // Pasif ama adet korunmuş hücre: soluk gösterilir (yeniden aktive edilince geri gelir)
                    cellText = isAdet && ayar.Adet > 0 ? ayar.Adet.ToString(CultureInfo.InvariantCulture) : "";

                var cellTb = new TextBlock
                {
                    Text = cellText,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse(
                        !odaDahil ? "#555555" : (efektifAktif ? (isAdet ? "#A8E090" : "#558855") : "#333333")))
                };

                var cell = new Border
                {
                    Tag = "datacell",
                    Background = new SolidColorBrush(Color.Parse(
                        !odaDahil ? "#1A1A1A" : (isCellSelected ? "#1C3C1A" : (efektifAktif ? "#050C06" : "#020402")))),
                    BorderBrush = new SolidColorBrush(Color.Parse(
                        !odaDahil ? "#333333" : (isCellSelected ? "#3C7A3A" : "#1A3A1A"))),
                    BorderThickness = new Avalonia.Thickness(isCellSelected ? 1.5 : 0.5),
                    Padding = new Avalonia.Thickness(2),
                    MinHeight = 24,
                    MinWidth = 46,
                    Child = cellTb,
                    Cursor = (odaDahil && !_vm.ProjeKilitli)
                        ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : null
                };

                var yakSatir = satir;
                var yakOdaId = oda.Id;
                var yakAyar = ayar;
                var yakOdaAd = oda.Ad;

                cell.PointerPressed += (_, ev) =>
                {
                    if (!ev.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                    if (!oda.MahalListesineDahil) return;

                    // İlk tık = yalnız seç (özellik paneli açılır; kilitli projede de
                    // salt-okunur inceleme yapılabilir). Zaten seçili hücreye ikinci
                    // tık = aktif/pasif değiştir.
                    bool zatenSecili = ReferenceEquals(SeciliHucreAyar, yakAyar) &&
                                       ReferenceEquals(SeciliSatir, yakSatir);

                    _vm.MahalListesiOdaVurgula(yakOdaId);
                    SeciliOdaAd = yakOdaAd;
                    SeciliHucreAyar = yakAyar;
                    _tabloGuncelleniyor = true;
                    SeciliSatir = yakSatir;
                    _tabloGuncelleniyor = false;
                    OnPropertyChanged(nameof(EfektifAdetModu));

                    if (zatenSecili)
                    {
                        if (KilitliyseDur()) return;
                        yakAyar.Aktif = !yakAyar.Aktif;

                        // Aktifleşirken Adet modunda hiç adet yoksa 1'e çek.
                        // Pasifleşirken Adet SIFIRLANMAZ — kullanıcının girdiği değer
                        // korunur, hücre yeniden aktive edilince geri gelir.
                        if (yakAyar.Aktif)
                        {
                            var (currentYerlesim, _, _, _) = yakSatir.EfektifAyarlar(yakOdaId);
                            if (currentYerlesim == "Adet" && yakAyar.Adet == 0)
                                yakAyar.Adet = 1;
                        }

                        // Sürükle-boya başlat: bu tıklamadaki YENİ durum, sürükleme
                        // boyunca geçilen hücrelere uygulanır (Excel doldurma hissi).
                        _boyaAktif = true;
                        _boyaDegeri = yakAyar.Aktif;
                    }

                    // Yalnız tablo durumunu/görseli güncelle; yerleşim butona basılınca yapılır.
                    GorselleriGuncelle();
                };

                // Sürükle-boya: basılı tutup üzerinden geçilen hücreler boyanır.
                cell.PointerEntered += (_, _) =>
                {
                    if (!_boyaAktif || !oda.MahalListesineDahil) return;
                    if (_vm.ProjeKilitli) return;
                    if (yakAyar.Aktif == _boyaDegeri) return;

                    yakAyar.Aktif = _boyaDegeri;
                    if (_boyaDegeri)
                    {
                        var (yer, _, _, _) = yakSatir.EfektifAyarlar(yakOdaId);
                        if (yer == "Adet" && yakAyar.Adet == 0) yakAyar.Adet = 1;
                    }
                    _vm.Kirlet();
                    GorselleriGuncelle();
                };

                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);    // ← c (gridSag'da 0'dan başlar)
                gridSag.Children.Add(cell);
            }
        }

        KatOzetiGuncelle();
        TabloHostSol.Child = gridSol;
        TabloHostSag.Child = gridSag;
    }

    /// Satırın kat toplamı: aktif oda sayısı + TAHMİNİ toplam adet (Grid/Duvar
    /// hücrelerinde aday üreticiden hesaplanır) + varsa toplam güç.
    private string SatirToplamMetni(BpcMahalSatiri satir, List<BasitProjeOda> odalar)
    {
        int aktifOda = 0, toplamAdet = 0;
        foreach (var oda in odalar)
        {
            if (!oda.MahalListesineDahil || !satir.EfektifAktif(oda.Id)) continue;
            aktifOda++;
            toplamAdet += MahalListesiServisi.YerlesimTahmini(satir, oda, _geometri);
        }
        if (aktifOda == 0) return "—";
        string metin = toplamAdet > 0 ? $"{aktifOda} oda · ~{toplamAdet} ad" : $"{aktifOda} oda";
        if (satir.GelenGucW is > 0 && toplamAdet > 0)
            metin += $" · {satir.GelenGucW * toplamAdet:N0} W";
        return metin;
    }

    /// Hücrede gösterilecek metin. Grid/Duvar aralıklı hücrelerde ✓ yerine
    /// tahmini adet (~N) yazılır — kullanıcı yerleştirmeden önce sonucu görür.
    private string HucreMetni(BpcMahalSatiri satir, BasitProjeOda oda,
        BpcMahalHucreAyar ayar, bool isAdet, bool odaDahil, bool efektifAktif)
    {
        if (!odaDahil) return "✕";

        if (efektifAktif)
        {
            if (isAdet) return ayar.Adet > 0 ? ayar.Adet.ToString(CultureInfo.InvariantCulture) : "✓";
            int tahmin = MahalListesiServisi.YerlesimTahmini(satir, oda, _geometri);
            return tahmin > 0 ? $"~{tahmin}" : "✓";
        }

        // Pasif ama adet korunmuş hücre: soluk gösterilir (yeniden aktive edilince geri gelir)
        return isAdet && ayar.Adet > 0 ? ayar.Adet.ToString(CultureInfo.InvariantCulture) : "";
    }

    /// Alt bilgi bandı: katın tahmini eleman ve güç toplamı (pano öngörüsü).
    private void KatOzetiGuncelle()
    {
        var kat = SeciliKat;
        if (kat == null || KatOzetText == null) return;

        var satirlar = kat.MahalListesi.Where(s => AktifTipler.Contains(s.Tip)).ToList();
        int toplamEleman = satirlar.Sum(s =>
            MahalListesiServisi.SatirTahminToplami(s, kat.Odalar, _geometri));
        double toplamW = MahalListesiServisi.KatTahminiGucW(kat, _vm.Kutuphane, _geometri);
        int aktifOda = kat.Odalar.Count(o => o.MahalListesineDahil);

        KatOzetText.Text = toplamEleman == 0
            ? $"{aktifOda} oda dahil · henüz kalem seçilmedi"
            : $"Tahmin: {toplamEleman} eleman · {aktifOda} oda" +
              (toplamW > 0 ? $" · ~{toplamW:N0} W kurulu güç" : "");
    }

    private void GorselleriGuncelle()
    {
        var gridSol = TabloHostSol.Child as Grid;
        var gridSag = TabloHostSag.Child as Grid;
        if (gridSol == null && gridSag == null) return;

        int rowIndex = 1;
        var satirlar = SeciliKat?.MahalListesi.Where(s => AktifTipler.Contains(s.Tip)).ToList() ?? new();
        var odalar = SeciliKat is { } gKat ? GosterilecekOdalar(gKat) : new();

        foreach (var satir in satirlar)
        {
            // Toplam sütunu → gridSol col 2
            var toplamHucre = gridSol?.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag is string tt && tt == "totalcell"
                                  && Grid.GetRow(b) == rowIndex && Grid.GetColumn(b) == 2);
            if (toplamHucre?.Child is TextBlock toplamTb)
                toplamTb.Text = SatirToplamMetni(satir, odalar);

            // Ad hücresi → gridSol col 1
            var adHucre = gridSol?.Children.OfType<Border>()
                .FirstOrDefault(b => !(b.Tag is string) && Grid.GetRow(b) == rowIndex && Grid.GetColumn(b) == 1);
            if (adHucre != null)
            {
                bool isAdSelected = (satir == SeciliSatir && SeciliHucreAyar == null);
                adHucre.Background = new SolidColorBrush(Color.Parse(isAdSelected ? "#1C3C1A" : "#0A1A0C"));
                adHucre.BorderBrush = new SolidColorBrush(Color.Parse(isAdSelected ? "#3C7A3A" : "#1A3A1A"));
                adHucre.BorderThickness = new Avalonia.Thickness(isAdSelected ? 1.5 : 0.5);
                if (adHucre.Child is TextBlock tb)
                    tb.Foreground = new SolidColorBrush(Color.Parse(isAdSelected ? "#FFFFFF" : "#DDEED2"));
            }

            int colIndex = 0;   // gridSag'da 0'dan başlar
            foreach (var oda in odalar)
            {
                var cell = gridSag?.Children.OfType<Border>()
                    .FirstOrDefault(b => b.Tag is string t && t == "datacell"
                                      && Grid.GetRow(b) == rowIndex && Grid.GetColumn(b) == colIndex);
                if (cell != null)
                {
                    var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
                    var (efektifYerlesim, _, _, _) = satir.EfektifAyarlar(oda.Id);
                    bool isAdet = efektifYerlesim == "Adet";
                    bool isCellSelected = (satir == SeciliSatir && ayar == SeciliHucreAyar);
                    bool odaDahil = oda.MahalListesineDahil;
                    bool efektifAktif = satir.EfektifAktif(oda.Id);

                    string cellText = HucreMetni(satir, oda, ayar, isAdet, odaDahil, efektifAktif);

                    cell.Background = new SolidColorBrush(Color.Parse(
                        !odaDahil ? "#1A1A1A" : (isCellSelected ? "#1C3C1A" : (efektifAktif ? "#050C06" : "#020402"))));
                    cell.BorderBrush = new SolidColorBrush(Color.Parse(
                        !odaDahil ? "#333333" : (isCellSelected ? "#3C7A3A" : "#1A3A1A")));
                    cell.BorderThickness = new Avalonia.Thickness(isCellSelected ? 1.5 : 0.5);

                    if (cell.Child is TextBlock cellTb)
                    {
                        cellTb.Text = cellText;
                        cellTb.Foreground = new SolidColorBrush(Color.Parse(
                            !odaDahil ? "#555555" : (efektifAktif ? (isAdet ? "#A8E090" : "#558855") : "#333333")));
                    }
                }
                colIndex++;
            }
            rowIndex++;
        }

        KatOzetiGuncelle();
    }

    private static Border BaslikHucre(string metin, int row, int col, int colSpan)
    {
        var b = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0E2211")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1A3A1A")),
            BorderThickness = new Avalonia.Thickness(0.5),
            Padding = new Avalonia.Thickness(8, 6),
            Child = new TextBlock
            {
                Text = metin,
                Foreground = new SolidColorBrush(Color.Parse("#78A060")),
                FontSize = 11, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            }
        };
        Grid.SetRow(b, row);
        Grid.SetColumn(b, col);
        Grid.SetColumnSpan(b, colSpan);
        return b;
    }

    private static Border AdHucre(string metin, int row, bool isSelected = false)
    {
        var b = new Border
        {
            Background = new SolidColorBrush(Color.Parse(isSelected ? "#1C3C1A" : "#0A1A0C")),
            BorderBrush = new SolidColorBrush(Color.Parse(isSelected ? "#3C7A3A" : "#1A3A1A")),
            BorderThickness = new Avalonia.Thickness(isSelected ? 1.5 : 0.5),
            Padding = new Avalonia.Thickness(8, 4),
            Child = new TextBlock
            {
                Text = metin,
                Foreground = new SolidColorBrush(Color.Parse(isSelected ? "#FFFFFF" : "#DDEED2")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(b, row);
        Grid.SetColumn(b, 1);
        return b;
    }

    // ── Üretkenlik komutları (codex12 §20) ──────────────────────────────────

    private async void KattanKopyala_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        var hedef = SeciliKat;
        if (hedef == null) { _vm.DurumMesaji = "Önce kat seçin."; return; }

        var adaylar = _vm.Katlar.Where(k => !ReferenceEquals(k, hedef) && k.MahalListesi.Count > 0).ToList();
        if (adaylar.Count == 0)
        {
            _vm.DurumMesaji = "Kopyalanacak mahal listesi olan başka kat yok.";
            return;
        }

        var kaynak = await SecimDiyalogu(
            "Kattan Kopyala",
            $"'{hedef.Ad}' katına hangi katın mahal listesi kopyalansın?\n" +
            "Odalar ADA göre eşleşir; sizin açtığınız hücreler korunur.",
            adaylar, k => $"{k.Ad}  ({k.MahalListesi.Count} kalem, {k.Odalar.Count} oda)");
        if (kaynak == null) return;

        var ozet = _vm.MahalKattanKopyala(kaynak, hedef);
        KalemComboDoldur();
        TabloKur();
        await BilgiDiyalogu("Kattan Kopyala", ozet);
    }

    private async void AkilliDoldur_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        var kat = SeciliKat;
        if (kat == null) { _vm.DurumMesaji = "Önce kat seçin."; return; }

        var ozet = _vm.MahalAkilliDoldur(kat, _zayif);
        KalemComboDoldur();
        TabloKur();
        await BilgiDiyalogu("Akıllı Doldurma", ozet +
            "\n\nNot: Yalnız boş hücreler dolduruldu; elle açtığınız hücrelere dokunulmadı." +
            "\nYerleşim için \"🗺 Yerleştir\" butonunu kullanın.");
    }

    private async void SablonKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kat = SeciliKat;
        if (kat == null || kat.MahalListesi.Count == 0)
        {
            _vm.DurumMesaji = "Kaydedilecek mahal listesi yok.";
            return;
        }

        var ad = await MetinDiyalogu("Şablon Kaydet",
            "Şablon adı (aynı ad varsa üzerine yazılır):", $"{kat.Ad} listesi");
        if (string.IsNullOrWhiteSpace(ad)) return;

        var mesaj = _vm.MahalSablonKaydet(kat, ad!);
        await BilgiDiyalogu("Şablon Kaydet", mesaj);
    }

    private async void SablonYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        var kat = SeciliKat;
        if (kat == null) { _vm.DurumMesaji = "Önce kat seçin."; return; }

        var dosya = MahalListesiServisi.SablonlariYukle();
        if (dosya.Sablonlar.Count == 0)
        {
            await BilgiDiyalogu("Şablondan Yükle",
                "Kayıtlı şablon yok. Önce bir katın listesini \"💾 Şablon Kaydet\" ile saklayın.");
            return;
        }

        var secilen = await SecimDiyalogu(
            "Şablondan Yükle",
            $"'{kat.Ad}' katına hangi şablon uygulansın?\nOdalar ADA göre eşleşir; açtığınız hücreler korunur.",
            dosya.Sablonlar,
            s => $"{s.Ad}  ({s.Satirlar.Count} kalem · {s.OlusturmaTarihi:dd.MM.yyyy})");
        if (secilen == null) return;

        var ozet = _vm.MahalSablonUygula(kat, secilen);
        KalemComboDoldur();
        TabloKur();
        await BilgiDiyalogu("Şablondan Yükle", ozet);
    }

    // ── Basit diyalog yardımcıları ──────────────────────────────────────────

    private async System.Threading.Tasks.Task<T?> SecimDiyalogu<T>(
        string baslik, string mesaj, IReadOnlyList<T> secenekler, Func<T, string> etiket) where T : class
    {
        var liste = new ListBox
        {
            ItemsSource = secenekler.Select(etiket).ToList(),
            SelectedIndex = 0,
            Height = Math.Min(240, 28 * secenekler.Count + 12),
            FontSize = 12
        };
        var tamam = new Button { Content = "Uygula", IsDefault = true, Background = new SolidColorBrush(Color.Parse("#1E3A12")), Foreground = Brushes.White };
        var iptal = new Button { Content = "İptal", IsCancel = true };

        var pencere = new Window
        {
            Title = baslik,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#081009")),
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = mesaj, Foreground = new SolidColorBrush(Color.Parse("#DDEED2")),
                                    FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    liste,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { iptal, tamam }
                    }
                }
            }
        };

        T? sonuc = null;
        tamam.Click += (_, _) =>
        {
            int i = liste.SelectedIndex;
            if (i >= 0 && i < secenekler.Count) sonuc = secenekler[i];
            pencere.Close();
        };
        iptal.Click += (_, _) => pencere.Close();

        await pencere.ShowDialog(this);
        return sonuc;
    }

    private async System.Threading.Tasks.Task<string?> MetinDiyalogu(string baslik, string mesaj, string varsayilan)
    {
        var kutu = new TextBox { Text = varsayilan, FontSize = 12, Height = 30 };
        var tamam = new Button { Content = "Kaydet", IsDefault = true, Background = new SolidColorBrush(Color.Parse("#1E3A12")), Foreground = Brushes.White };
        var iptal = new Button { Content = "İptal", IsCancel = true };

        var pencere = new Window
        {
            Title = baslik,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#081009")),
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = mesaj, Foreground = new SolidColorBrush(Color.Parse("#DDEED2")),
                                    FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    kutu,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { iptal, tamam }
                    }
                }
            }
        };

        string? sonuc = null;
        tamam.Click += (_, _) => { sonuc = kutu.Text; pencere.Close(); };
        iptal.Click += (_, _) => pencere.Close();

        await pencere.ShowDialog(this);
        return sonuc;
    }

    private async System.Threading.Tasks.Task<bool> OnayDiyalogu(string baslik, string mesaj)
    {
        var tamam = new Button
        {
            Content = "Devam Et", IsDefault = true,
            Background = new SolidColorBrush(Color.Parse("#1E4A12")), Foreground = Brushes.White
        };
        var iptal = new Button { Content = "İptal", IsCancel = true };
        var pencere = new Window
        {
            Title = baslik,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#081009")),
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = mesaj, Foreground = new SolidColorBrush(Color.Parse("#DDEED2")),
                                    FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { iptal, tamam }
                    }
                }
            }
        };

        bool sonuc = false;
        tamam.Click += (_, _) => { sonuc = true; pencere.Close(); };
        iptal.Click += (_, _) => pencere.Close();
        await pencere.ShowDialog(this);
        return sonuc;
    }

    private async System.Threading.Tasks.Task BilgiDiyalogu(string baslik, string mesaj)
    {
        var tamam = new Button
        {
            Content = "Tamam", IsDefault = true, IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new SolidColorBrush(Color.Parse("#1E3A12")), Foreground = Brushes.White
        };
        var pencere = new Window
        {
            Title = baslik,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#081009")),
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = mesaj, Foreground = new SolidColorBrush(Color.Parse("#DDEED2")),
                                    FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    tamam
                }
            }
        };
        tamam.Click += (_, _) => pencere.Close();
        await pencere.ShowDialog(this);
    }

    // ── Oda filtreleme / sıralama ───────────────────────────────────────────

    private string _odaFiltre = "";
    private int _odaSiraModu; // 0=tespit, 1=ad, 2=alan

    private void OdaArama_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _odaFiltre = OdaAramaBox.Text ?? "";
        TabloKur();
    }

    private void OdaSira_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor) return;
        _odaSiraModu = OdaSiraCombo.SelectedIndex;
        TabloKur();
    }

    /// Tabloda gösterilecek odalar: arama filtresi + seçili sıralama.
    private List<BasitProjeOda> GosterilecekOdalar(BasitProjeKat kat)
    {
        IEnumerable<BasitProjeOda> q = kat.Odalar;

        if (!string.IsNullOrWhiteSpace(_odaFiltre))
        {
            var f = MahalListesiServisi.OdaAdiNormalize(_odaFiltre);
            if (f.Length > 0)
                q = q.Where(o => MahalListesiServisi.OdaAdiNormalize(o.Ad).Contains(f, StringComparison.Ordinal));
        }

        q = _odaSiraModu switch
        {
            1 => q.OrderBy(o => o.Ad, StringComparer.CurrentCultureIgnoreCase),
            2 => q.OrderByDescending(o => o.AlanM2),
            _ => q
        };
        return q.ToList();
    }

    private async void TumKatlaraYerlestir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        var hedefler = _vm.Katlar.Where(k => k.MahalListesi.Count > 0).ToList();
        if (hedefler.Count == 0)
        {
            await BilgiDiyalogu("Tüm Katlar", "Mahal listesi olan kat yok.");
            return;
        }

        bool tamYenileme = SadecaEksikleriCheck.IsChecked != true;
        var onay = await OnayDiyalogu("Tüm Katlara Yerleştir",
            $"{hedefler.Count} katta yerleştirme çalıştırılacak:\n" +
            string.Join("\n", hedefler.Select(k => $"  • {k.Ad} ({k.MahalListesi.Count} kalem)")) +
            (tamYenileme
                ? "\n\n⚠ TAM YENİLEME modu: her katta eski otomatik elemanlar silinip yeniden üretilir; bağlı kablolar da silinir."
                : "\n\nYalnız eksikler eklenecek; mevcut elemanlar korunur.") +
            "\n\nDevam edilsin mi?");
        if (!onay) return;

        _vm.SadecaEksikleriEkle = !tamYenileme;
        var rapor = _vm.TumKatlaraYerlestir();

        KatComboDoldur();
        KalemComboDoldur();
        TabloKur();
        await BilgiDiyalogu("Tüm Katlar — Sonuç", rapor);
    }

    private void OtomatikYerlestir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KilitliyseDur()) return;
        if (SeciliKat is { } kat)
            _vm.SeciliKat = kat;
        _vm.SadecaEksikleriEkle = SadecaEksikleriCheck.IsChecked == true;
        _vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
    }

    private bool KilitliyseDur()
    {
        if (!_vm.ProjeKilitli) return false;
        _vm.DurumMesaji = "Proje kilitli — mahal listesini düzenlemek için önce kilidi açın.";
        return true;
    }

    private static string TipAd(BpcElemanTipi t) => t switch
    {
        BpcElemanTipi.Armatür => "Aydınlatma Armatürü",
        BpcElemanTipi.SarkitArmatur => "Sarkıt Armatür",
        BpcElemanTipi.GlopArmatur => "Glop Armatür",
        BpcElemanTipi.CiplakLamba => "Çıplak Lamba",
        BpcElemanTipi.BantArmatur => "Bant Armatür",
        BpcElemanTipi.Avize => "Avize",
        BpcElemanTipi.WallWasher => "Duvar Yıkayıcı (Wall Washer)",
        BpcElemanTipi.Anahtar => "Anahtar",
        BpcElemanTipi.Komütatör => "Komütatör",
        BpcElemanTipi.UcluKomutator => "3'lü Komütatör",
        BpcElemanTipi.DarbeAnahtari => "Darbe Anahtarı",
        BpcElemanTipi.Vavien => "Vavien",
        BpcElemanTipi.KomütatörVavien => "Komütatör Vavien",
        BpcElemanTipi.Deviatör => "Deviatör",
        BpcElemanTipi.Dimmer => "Dimmer",
        BpcElemanTipi.IkiKutupluAnahtar => "İki Kutuplu Anahtar",
        BpcElemanTipi.Priz => "Priz",
        BpcElemanTipi.KapakliPriz => "Kapaklı Priz",
        BpcElemanTipi.SivaUstuPriz => "Sıvaüstü Priz",
        BpcElemanTipi.TrifazePriz => "Trifaze Priz",
        BpcElemanTipi.KombinePrizKutusu => "Kombine Priz Panosu",
        BpcElemanTipi.TavanPrizi => "Tavan Prizi",
        BpcElemanTipi.UsbliMasaPrizi => "USB'li Masa Prizi",
        BpcElemanTipi.UpsPrizi => "UPS Prizi",
        BpcElemanTipi.KapakliUpsPrizi => "Kapaklı UPS Prizi",
        BpcElemanTipi.SivaUstuUpsPrizi => "Sıvaüstü UPS Prizi",
        BpcElemanTipi.IkiliPriz => "İkili Priz",
        BpcElemanTipi.UcluPriz => "Üçlü Priz",
        BpcElemanTipi.Buat => "Buat",
        BpcElemanTipi.KatPanosu => "Kat Panosu",
        BpcElemanTipi.KlimaIcUnite => "Klima İç Ünite",
        BpcElemanTipi.KlimaDisUnite => "Klima Dış Ünite",
        BpcElemanTipi.DataPriz => "Data Prizi",
        BpcElemanTipi.TvPriz => "TV Prizi",
        BpcElemanTipi.TelefonPriz => "Telefon Prizi",
        BpcElemanTipi.Kamera => "Kamera",
        BpcElemanTipi.CctvKayit => "NVR / Kayıt Cihazı",
        BpcElemanTipi.AgAnahtari => "Ağ Anahtarı (Switch)",
        BpcElemanTipi.Hoparlor => "Hoparlör",
        BpcElemanTipi.AnonsAmfi => "Anons Amfisi",
        BpcElemanTipi.Diafon => "Diafon",
        BpcElemanTipi.KapiZili => "Kapı Zili",
        BpcElemanTipi.DiafonSantrali => "Diafon Santrali",
        BpcElemanTipi.DumanDedektor => "Duman Dedektörü",
        BpcElemanTipi.IsiDedektor => "Isı Dedektörü",
        BpcElemanTipi.KombineDedektor => "Kombine Dedektör",
        BpcElemanTipi.YanginButonu => "Yangın Butonu",
        BpcElemanTipi.Siren => "Siren",
        BpcElemanTipi.Flasor => "Flaşör",
        BpcElemanTipi.YanginSantrali => "Yangın Santrali",
        BpcElemanTipi.DataRack => "Data Rack",
        BpcElemanTipi.KatDagitimKutusu => "Kat Dağıtım Kutusu",
        BpcElemanTipi.TvMerkezi => "TV Merkezi",
        BpcElemanTipi.TelefonDagitim => "Telefon Dağıtım (MDF)",
        _ => t.ToString()
    };
}
